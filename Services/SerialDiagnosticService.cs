using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Media;
using AnalisePro.Models;

namespace AnalisePro.Services
{
    public class SerialDiagnosticService : IDisposable
    {
        private SerialPort _currentPort;
        public Action<byte[], string, string> OnDataReceived;
        public Action<string> OnError;

        private static readonly SolidColorBrush BrushGreen = FreezeBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        private static readonly SolidColorBrush BrushCyan = FreezeBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush BrushOrange = FreezeBrush(Color.FromRgb(0xFF, 0xB7, 0x03));
        private static readonly SolidColorBrush BrushRed = FreezeBrush(Color.FromRgb(0xFF, 0x33, 0x66));

        private static SolidColorBrush FreezeBrush(Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        public static List<SerialPortInfoItem> GetAvailableSerialPorts()
        {
            var list = new List<SerialPortInfoItem>();

            try
            {
                // 1. Obter nomes registrados no sistema (COM1, COM2...)
                var registeredNames = SerialPort.GetPortNames().Distinct().OrderBy(p => p).ToList();

                // 2. Consultar WMI para obter os detalhes de hardware (placas PCI x1, adaptadores USB, etc.)
                var pnpDict = new Dictionary<string, SerialPortInfoItem>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE ClassGuid = '{4d36e978-e325-11ce-bfc1-08002be10318}' OR Caption LIKE '%(COM%'"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string caption = (obj["Caption"] ?? "").ToString();
                            string name = (obj["Name"] ?? "").ToString();
                            string pnpId = (obj["PNPDeviceID"] ?? "").ToString();
                            string status = (obj["Status"] ?? "OK").ToString();
                            string manufacturer = (obj["Manufacturer"] ?? "").ToString();

                            // Extrair o nome da porta (ex: COM1) de dentro do caption "(COM1)"
                            string portName = ExtractPortName(caption);
                            if (string.IsNullOrEmpty(portName))
                            {
                                portName = ExtractPortName(name);
                            }

                            if (!string.IsNullOrEmpty(portName))
                            {
                                var item = new SerialPortInfoItem
                                {
                                    PortName = portName,
                                    Description = string.IsNullOrEmpty(caption) ? name : caption,
                                    Manufacturer = string.IsNullOrEmpty(manufacturer) ? "Genérico" : manufacturer,
                                    HardwareId = pnpId,
                                    Status = status,
                                    IsPciCard = pnpId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase),
                                    StatusBrush = status.Equals("OK", StringComparison.OrdinalIgnoreCase) ? BrushGreen : BrushRed
                                };

                                if (item.IsPciCard)
                                {
                                    item.CardTypeDisplay = "Placa PCI / PCIe Serial (RS-232)";
                                }
                                else if (pnpId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                                {
                                    item.CardTypeDisplay = "Adaptador USB para Serial";
                                }
                                else
                                {
                                    item.CardTypeDisplay = "Porta Serial Nativa da Placa-Mãe";
                                }

                                pnpDict[portName] = item;
                            }
                        }
                    }
                }
                catch { }

                // 3. Montar a lista final integrando com os nomes do sistema
                foreach (var portName in registeredNames)
                {
                    if (pnpDict.ContainsKey(portName))
                    {
                        list.Add(pnpDict[portName]);
                    }
                    else
                    {
                        list.Add(new SerialPortInfoItem
                        {
                            PortName = portName,
                            Description = "Porta Serial Padrão (" + portName + ")",
                            Manufacturer = "Dispositivo de Comunicação",
                            HardwareId = "DESCONHECIDO",
                            Status = "OK",
                            IsPciCard = false,
                            CardTypeDisplay = "Porta COM Ativa",
                            StatusBrush = BrushCyan
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        private static string ExtractPortName(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int idxOpen = text.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
            if (idxOpen >= 0)
            {
                int idxClose = text.IndexOf(')', idxOpen);
                if (idxClose > idxOpen)
                {
                    return text.Substring(idxOpen + 1, idxClose - idxOpen - 1).Trim();
                }
            }
            return null;
        }

        // ================= CONEXÃO E TESTES DE BALANÇA / I/O =================

        public bool IsOpen
        {
            get { return _currentPort != null && _currentPort.IsOpen; }
        }

        public string ActivePortName
        {
            get { return _currentPort != null ? _currentPort.PortName : null; }
        }

        public void OpenPort(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, Handshake handshake, bool enableDtr, bool enableRts)
        {
            ClosePort();

            try
            {
                _currentPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    Handshake = handshake,
                    DtrEnable = enableDtr, // CRUCIAL para alimentar balanças seriais
                    RtsEnable = enableRts, // CRUCIAL para controle de fluxo e liberação de dados
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _currentPort.DataReceived += Port_DataReceived;
                _currentPort.Open();
            }
            catch (Exception ex)
            {
                ClosePort();
                throw new Exception(string.Format("Falha ao abrir a porta {0}: {1}", portName, ex.Message), ex);
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_currentPort == null || !_currentPort.IsOpen) return;

                int bytesToRead = _currentPort.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] buffer = new byte[bytesToRead];
                int read = _currentPort.Read(buffer, 0, bytesToRead);

                if (read > 0)
                {
                    string text = Encoding.ASCII.GetString(buffer, 0, read);
                    string hex = BitConverter.ToString(buffer, 0, read).Replace("-", " ");

                    if (OnDataReceived != null)
                    {
                        OnDataReceived(buffer, text, hex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (OnError != null)
                {
                    OnError(ex.Message);
                }
            }
        }

        // Envia o byte ENQ (0x05) padrão das balanças Toledo Prix 3, 4, 5, Elgin e Balmak
        public void SendEnqCommand()
        {
            if (!IsOpen) throw new InvalidOperationException("A porta serial não está aberta.");
            byte[] cmd = new byte[] { 0x05 };
            _currentPort.Write(cmd, 0, 1);
        }

        // Envia o comando Filizola (P + Enter)
        public void SendFilizolaCommand()
        {
            if (!IsOpen) throw new InvalidOperationException("A porta serial não está aberta.");
            byte[] cmd = Encoding.ASCII.GetBytes("P\r\n");
            _currentPort.Write(cmd, 0, cmd.Length);
        }

        // Envia comando em texto ASCII
        public void SendTextCommand(string text)
        {
            if (!IsOpen) throw new InvalidOperationException("A porta serial não está aberta.");
            byte[] cmd = Encoding.ASCII.GetBytes(text);
            _currentPort.Write(cmd, 0, cmd.Length);
        }

        // Envia bytes Hexadecimais
        public void SendHexCommand(byte[] bytes)
        {
            if (!IsOpen) throw new InvalidOperationException("A porta serial não está aberta.");
            if (bytes != null && bytes.Length > 0)
            {
                _currentPort.Write(bytes, 0, bytes.Length);
            }
        }

        public string GetPinStatus()
        {
            if (!IsOpen) return "Porta Desconectada";
            try
            {
                return string.Format("CTS: {0} • DSR: {1} • CD: {2}",
                    _currentPort.CtsHolding ? "ALTO (1)" : "BAIXO (0)",
                    _currentPort.DsrHolding ? "ALTO (1)" : "BAIXO (0)",
                    _currentPort.CDHolding ? "ALTO (1)" : "BAIXO (0)");
            }
            catch
            {
                return "Leitura de pinos não suportada pelo driver da placa.";
            }
        }

        public void ClosePort()
        {
            try
            {
                if (_currentPort != null)
                {
                    if (_currentPort.IsOpen)
                    {
                        _currentPort.DataReceived -= Port_DataReceived;
                        _currentPort.Close();
                    }
                    _currentPort.Dispose();
                }
            }
            catch { }
            finally
            {
                _currentPort = null;
            }
        }

        public void Dispose()
        {
            ClosePort();
        }
    }
}
