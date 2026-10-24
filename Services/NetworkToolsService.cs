using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AnalisePro.Models;

namespace AnalisePro.Services
{
    public static class NetworkToolsService
    {
        // P/Invoke SendARP nativo do Windows para capturar endereço MAC instantâneo
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestIP, uint SrcIP, byte[] pMacAddr, ref int PhysAddrLen);

        private static readonly SolidColorBrush BrushGreen = FreezeBrush(Color.FromRgb(0x00, 0xE6, 0x76));
        private static readonly SolidColorBrush BrushCyan = FreezeBrush(Color.FromRgb(0x00, 0xF0, 0xFF));
        private static readonly SolidColorBrush BrushOrange = FreezeBrush(Color.FromRgb(0xFF, 0xB7, 0x03));
        private static readonly SolidColorBrush BrushRed = FreezeBrush(Color.FromRgb(0xFF, 0x33, 0x66));
        private static readonly SolidColorBrush BrushMuted = FreezeBrush(Color.FromRgb(0x64, 0x74, 0x8B));

        private static SolidColorBrush FreezeBrush(Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        // ================= 1. DIAGNÓSTICO DO CABO RJ45 E VELOCIDADE =================

        public static List<CableDiagnosticInfo> GetCableDiagnostics()
        {
            var list = new List<CableDiagnosticInfo>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var nic in interfaces)
                {
                    // Apenas interfaces físicas Ethernet ou Wi-Fi
                    if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    {
                        continue;
                    }

                    // Ignorar adaptadores virtuais comuns (Hyper-V, VirtualBox, VMware, Npcap)
                    string desc = nic.Description.ToLower();
                    if (desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("pseudo") || desc.Contains("loopback"))
                    {
                        continue;
                    }

                    var info = new CableDiagnosticInfo();
                    info.AdapterName = nic.Name;
                    info.AdapterDescription = nic.Description;
                    info.IsConnected = nic.OperationalStatus == OperationalStatus.Up;

                    bool isGigabitHardware = IsGigabitCapableHardware(nic.Description);
                    info.IsGigabitCapable = isGigabitHardware;

                    if (!info.IsConnected)
                    {
                        info.LinkSpeedDisplay = "Cabo Desconectado / Inativo";
                        info.StatusBrush = BrushMuted;
                        info.WarningTitle = "Adaptador Desconectado";
                        info.WarningDescription = "Nenhum sinal detectado na porta de rede.";
                        info.PinDiagnosticMessage = "Verifique se o cabo RJ45 está plugado firmemente até ouvir o clique.";
                        list.Add(info);
                        continue;
                    }

                    long speedBps = nic.Speed;
                    info.LinkSpeedBps = speedBps;

                    // Diagnóstico de velocidade e pinos
                    if (speedBps >= 1000000000) // 1 Gbps ou mais
                    {
                        info.LinkSpeedDisplay = string.Format("{0:F1} Gbps (Gigabit Full-Duplex)", speedBps / 1000000000.0);
                        info.StatusBrush = BrushGreen;
                        info.HasCableWarning = false;
                        info.WarningTitle = "Conexão Gigabit Operando a 100%";
                        info.PinDiagnosticMessage = "Todos os 8 pinos do conector RJ45 (4 pares) estão operando em velocidade máxima de 1 Gbps.";
                    }
                    else if (speedBps >= 100000000) // 100 Mbps
                    {
                        info.LinkSpeedDisplay = "100 Mbps (Fast Ethernet)";

                        if (isGigabitHardware)
                        {
                            // Alerta de cabo: placa Gigabit conectada a 100 Mbps
                            info.StatusBrush = BrushOrange;
                            info.HasCableWarning = true;
                            info.WarningTitle = "Atenção: Link limitado a 100 Mbps em Placa Gigabit!";
                            info.WarningDescription = "A placa suporta 1000 Mbps (1 Gbps), mas negociou apenas 100 Mbps com o switch/roteador.";
                            info.PinDiagnosticMessage = "Diagnóstico do Cabo RJ45: Para atingir 1 Gbps, são obrigatórios todos os 8 pinos do conector (pares verde, laranja, azul e marrom). Em 100 Mbps, apenas os pinos 1, 2, 3 e 6 são utilizados. Provável rompimento ou mau contato nos pinos 4, 5, 7 ou 8 (ou switch limitado a 100M).";
                        }
                        else
                        {
                            info.StatusBrush = BrushCyan;
                            info.HasCableWarning = false;
                            info.WarningTitle = "Link 100 Mbps Normal para o hardware";
                            info.PinDiagnosticMessage = "Adaptador operando no limite de especificação do hardware (100 Mbps).";
                        }
                    }
                    else if (speedBps > 0) // 10 Mbps
                    {
                        info.LinkSpeedDisplay = "10 Mbps (Velocidade Muito Baixa)";
                        info.StatusBrush = BrushRed;
                        info.HasCableWarning = true;
                        info.WarningTitle = "Alerta Crítico: Conexão rebaixada para 10 Mbps!";
                        info.WarningDescription = "Degradação extrema no sinal elétrico ou cabo danificado.";
                        info.PinDiagnosticMessage = "O cabo de rede apresenta sério defeito físico de crimpagem, rompimento interno de vias ou oxidação grave nos pinos do conector.";
                    }
                    else
                    {
                        info.LinkSpeedDisplay = "Velocidade Desconhecida";
                        info.StatusBrush = BrushMuted;
                    }

                    // Estatísticas de pacotes e erros
                    try
                    {
                        var stats = nic.GetIPv4Statistics();
                        info.PacketsReceivedErrors = stats.IncomingPacketsWithErrors;
                        info.PacketsDiscarded = stats.IncomingPacketsDiscarded;

                        if (info.PacketsReceivedErrors > 0)
                        {
                            info.PinDiagnosticMessage += string.Format(" (Detectados {0} pacotes com erro de CRC/quadro - possível ruído no cabo).", info.PacketsReceivedErrors);
                        }
                    }
                    catch { }

                    list.Add(info);
                }
            }
            catch { }

            return list;
        }

        private static bool IsGigabitCapableHardware(string description)
        {
            if (string.IsNullOrEmpty(description)) return false;
            string d = description.ToLower();

            if (d.Contains("gigabit") || d.Contains("gbe") || d.Contains("10/100/1000") ||
                d.Contains("1gb") || d.Contains("2.5g") || d.Contains("rtl8111") ||
                d.Contains("rtl8168") || d.Contains("i219") || d.Contains("i211") ||
                d.Contains("i225") || d.Contains("killer"))
            {
                return true;
            }
            return false;
        }

        // ================= 2. TESTE DE CONECTIVIDADE EM 3 PONTOS =================

        public static async Task<List<ConnectivityCheckResult>> RunConnectivityDiagnosticAsync()
        {
            var results = new List<ConnectivityCheckResult>();

            // Alvo 1: Gateway Padrão Local (Roteador)
            string gatewayIp = GetDefaultGatewayIp();
            var resGateway = new ConnectivityCheckResult
            {
                TargetName = "1. Gateway Local (Roteador)",
                TargetAddress = string.IsNullOrEmpty(gatewayIp) ? "Não Detectado" : gatewayIp
            };

            if (!string.IsNullOrEmpty(gatewayIp))
            {
                resGateway = await PingTargetAsync(resGateway.TargetName, gatewayIp);
            }
            else
            {
                resGateway.StatusDisplay = "Sem Gateway Ativo";
                resGateway.StatusBrush = BrushRed;
            }
            results.Add(resGateway);

            // Alvo 2: DNS Público (Cloudflare 1.1.1.1)
            var resDns = await PingTargetAsync("2. Provedor / WAN (Cloudflare 1.1.1.1)", "1.1.1.1");
            results.Add(resDns);

            // Alvo 3: Resolução de Nomes e Web (Google)
            var resWeb = await PingTargetAsync("3. Resolução DNS Web (google.com)", "google.com");
            results.Add(resWeb);

            return results;
        }

        private static async Task<ConnectivityCheckResult> PingTargetAsync(string name, string address)
        {
            var res = new ConnectivityCheckResult
            {
                TargetName = name,
                TargetAddress = address
            };

            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(address, 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        res.LatencyMs = reply.RoundtripTime;
                        res.LatencyDisplay = string.Format("{0} ms", reply.RoundtripTime);

                        if (reply.RoundtripTime < 35)
                        {
                            res.StatusDisplay = "Excelente";
                            res.StatusBrush = BrushGreen;
                        }
                        else if (reply.RoundtripTime < 100)
                        {
                            res.StatusDisplay = "Bom";
                            res.StatusBrush = BrushCyan;
                        }
                        else
                        {
                            res.StatusDisplay = "Lento";
                            res.StatusBrush = BrushOrange;
                        }
                    }
                    else
                    {
                        res.LatencyDisplay = "Falha";
                        res.StatusDisplay = string.Format("Sem Resposta ({0})", reply.Status);
                        res.StatusBrush = BrushRed;
                    }
                }
            }
            catch (Exception ex)
            {
                res.LatencyDisplay = "Erro";
                res.StatusDisplay = ex.Message;
                res.StatusBrush = BrushRed;
            }

            return res;
        }

        // ================= 3. SCANNER DE REDE LOCAL (IPs, MAC, NOME, TIPO) =================

        public static async Task ScanLocalSubnetAsync(
            Action<NetworkDeviceItem> onDeviceFound,
            Action<int> onProgressChanged,
            CancellationToken token)
        {
            string localIp = null;
            string subnetMask = null;

            // Obter IP e Máscara locais da placa ativa
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = nic.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                    {
                        localIp = unicast.Address.ToString();
                        if (unicast.IPv4Mask != null)
                        {
                            subnetMask = unicast.IPv4Mask.ToString();
                        }
                        break;
                    }
                }
                if (localIp != null) break;
            }

            if (string.IsNullOrEmpty(localIp))
            {
                return;
            }

            string gatewayIp = GetDefaultGatewayIp();

            // Calcular faixa /24 (padrão mais comum em redes domésticas e corporativas 192.168.x.x ou 10.x.x.x)
            string[] ipParts = localIp.Split('.');
            if (ipParts.Length != 4) return;
            string baseSubnet = string.Format("{0}.{1}.{2}.", ipParts[0], ipParts[1], ipParts[2]);

            // Adicionar a própria máquina local primeiro
            var localItem = new NetworkDeviceItem
            {
                IpAddress = localIp,
                HostName = Environment.MachineName + " (Este Computador)",
                MacAddress = GetLocalMacAddress(),
                Manufacturer = "Este Computador",
                DeviceType = "Computador Local",
                DeviceIcon = "💻",
                LatencyMs = 0,
                LatencyDisplay = "< 1 ms",
                OpenServices = "Ativo",
                IsLocalMachine = true,
                StatusBrush = BrushGreen
            };
            onDeviceFound(localItem);

            int totalHosts = 254;
            int scannedCount = 0;
            var semaphore = new SemaphoreSlim(25); // Até 25 pings concorrentes
            var tasks = new List<Task>();

            for (int i = 1; i <= totalHosts; i++)
            {
                if (token.IsCancellationRequested) break;

                string targetIp = baseSubnet + i;
                if (targetIp == localIp)
                {
                    Interlocked.Increment(ref scannedCount);
                    continue;
                }

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        bool isOnline = false;
                        long roundtrip = -1;

                        try
                        {
                            using (var ping = new Ping())
                            {
                                var reply = await ping.SendPingAsync(targetIp, 250);
                                if (reply.Status == IPStatus.Success)
                                {
                                    isOnline = true;
                                    roundtrip = reply.RoundtripTime;
                                }
                            }
                        }
                        catch { }

                        // Mesmo se ping estiver bloqueado por firewall, tentar SendARP (pega dispositivos silenciosos)
                        string mac = GetMacAddressFromArp(targetIp);
                        if (!string.IsNullOrEmpty(mac) && mac != "00:00:00:00:00:00")
                        {
                            isOnline = true;
                        }

                        if (isOnline)
                        {
                            var device = new NetworkDeviceItem
                            {
                                IpAddress = targetIp,
                                LatencyMs = roundtrip >= 0 ? roundtrip : 1,
                                LatencyDisplay = roundtrip >= 0 ? string.Format("{0} ms", roundtrip) : "Detectado",
                                MacAddress = string.IsNullOrEmpty(mac) ? "--" : mac,
                                StatusBrush = BrushGreen
                            };

                            // Verificar se é o roteador
                            if (targetIp == gatewayIp)
                            {
                                device.IsGateway = true;
                                device.DeviceType = "Roteador / Gateway Principal";
                                device.DeviceIcon = "🌐";
                            }

                            // Resolver fabricante pelo MAC
                            device.Manufacturer = ResolveManufacturer(device.MacAddress);

                            // Resolver Nome do Host em segundo plano
                            device.HostName = await ResolveHostNameAsync(targetIp);

                            // Inferir tipo e ícone
                            ClassifyDeviceType(device);

                            // Checagem rápida de serviços comuns
                            device.OpenServices = await CheckCommonServicesAsync(targetIp);

                            onDeviceFound(device);
                        }
                    }
                    finally
                    {
                        int current = Interlocked.Increment(ref scannedCount);
                        int pct = (int)((double)current / totalHosts * 100);
                        onProgressChanged(pct);
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private static string GetMacAddressFromArp(string ipString)
        {
            try
            {
                IPAddress ip;
                if (!IPAddress.TryParse(ipString, out ip)) return null;

                uint destIp = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
                byte[] macAddr = new byte[6];
                int macAddrLen = macAddr.Length;

                int result = SendARP(destIp, 0, macAddr, ref macAddrLen);
                if (result == 0 && macAddrLen == 6)
                {
                    return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                        macAddr[0], macAddr[1], macAddr[2], macAddr[3], macAddr[4], macAddr[5]);
                }
            }
            catch { }
            return null;
        }

        private static async Task<string> ResolveHostNameAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                if (entry != null && !string.IsNullOrEmpty(entry.HostName))
                {
                    return entry.HostName;
                }
            }
            catch { }

            return "Host Ativo";
        }

        private static string ResolveManufacturer(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length < 8) return "Fabricante Desconhecido";

            string prefix = mac.Substring(0, 8).ToUpper();

            // Mapeamento dos fabricantes mais frequentes no mercado
            if (prefix.StartsWith("00:1A:11") || prefix.StartsWith("F4:F5:D8")) return "Google";
            if (prefix.StartsWith("B8:27:EB") || prefix.StartsWith("DC:A6:32") || prefix.StartsWith("E4:5F:01")) return "Raspberry Pi";
            if (prefix.StartsWith("00:0C:29") || prefix.StartsWith("00:50:56")) return "VMware VM";
            if (prefix.StartsWith("00:15:5D")) return "Microsoft Hyper-V";
            if (prefix.StartsWith("00:11:32")) return "Synology NAS";

            // Dispositivos móveis e roteadores
            if (prefix.StartsWith("F0:18:98") || prefix.StartsWith("3C:06:30") || prefix.StartsWith("AC:BC:32") ||
                prefix.StartsWith("BC:D1:77") || prefix.StartsWith("98:01:A7") || prefix.StartsWith("A4:C3:61") ||
                prefix.StartsWith("F8:FF:C2")) return "Apple Inc.";

            if (prefix.StartsWith("48:5A:3F") || prefix.StartsWith("50:85:69") || prefix.StartsWith("54:04:A6") ||
                prefix.StartsWith("94:35:0A") || prefix.StartsWith("A8:9C:ED")) return "Samsung Electronics";

            if (prefix.StartsWith("50:C7:BF") || prefix.StartsWith("98:DA:C4") || prefix.StartsWith("14:CC:20") ||
                prefix.StartsWith("C0:25:E9") || prefix.StartsWith("E8:48:B8") || prefix.StartsWith("30:DE:4B")) return "TP-Link";

            if (prefix.StartsWith("00:1A:3F") || prefix.StartsWith("48:28:2F") || prefix.StartsWith("A0:2B:B8")) return "Intelbras";
            if (prefix.StartsWith("48:8F:5A") || prefix.StartsWith("64:D1:54") || prefix.StartsWith("CC:2D:E0")) return "MikroTik";
            if (prefix.StartsWith("78:8A:20") || prefix.StartsWith("F0:9F:C2") || prefix.StartsWith("24:A4:3C")) return "Ubiquiti Networks";
            if (prefix.StartsWith("00:1E:10") || prefix.StartsWith("E8:CD:2D") || prefix.StartsWith("44:6E:E5")) return "Huawei";

            if (prefix.StartsWith("00:14:22") || prefix.StartsWith("18:66:DA") || prefix.StartsWith("B8:AC:6F")) return "Dell Technologies";
            if (prefix.StartsWith("00:1B:78") || prefix.StartsWith("3C:D9:2B") || prefix.StartsWith("28:80:23")) return "HP Inc.";
            if (prefix.StartsWith("00:21:5C") || prefix.StartsWith("54:EE:75") || prefix.StartsWith("E0:D5:5E")) return "Lenovo";

            if (prefix.StartsWith("00:1E:67") || prefix.StartsWith("00:21:6A") || prefix.StartsWith("F8:63:3F")) return "Intel Corporation";
            if (prefix.StartsWith("00:E0:4C") || prefix.StartsWith("52:54:00") || prefix.StartsWith("00:1A:92")) return "Realtek Semiconductor";

            if (prefix.StartsWith("64:09:80") || prefix.StartsWith("7C:49:EB")) return "Xiaomi / Redmi";
            if (prefix.StartsWith("44:00:10") || prefix.StartsWith("A4:DA:32")) return "Amazon Technologies";

            return "Dispositivo de Rede (" + prefix + ")";
        }

        private static void ClassifyDeviceType(NetworkDeviceItem device)
        {
            if (device.IsGateway)
            {
                device.DeviceType = "Roteador / Gateway Principal";
                device.DeviceIcon = "🌐";
                return;
            }

            string man = (device.Manufacturer ?? "").ToLower();
            string host = (device.HostName ?? "").ToLower();

            if (man.Contains("apple") || man.Contains("samsung") || man.Contains("xiaomi") || host.Contains("iphone") || host.Contains("android") || host.Contains("galaxy"))
            {
                device.DeviceType = "Smartphone / Dispositivo Móvel";
                device.DeviceIcon = "📱";
            }
            else if (man.Contains("tp-link") || man.Contains("mikrotik") || man.Contains("ubiquiti") || man.Contains("huawei") || host.Contains("router") || host.Contains("gateway"))
            {
                device.DeviceType = "Roteador / Ponto de Acesso";
                device.DeviceIcon = "🌐";
            }
            else if (man.Contains("intelbras") || host.Contains("dvr") || host.Contains("camera") || host.Contains("cam"))
            {
                device.DeviceType = "Câmera CFTV / Gravador DVR";
                device.DeviceIcon = "📹";
            }
            else if (man.Contains("hp") && (host.Contains("print") || host.Contains("deskjet") || host.Contains("laserjet")))
            {
                device.DeviceType = "Impressora de Rede";
                device.DeviceIcon = "🖨️";
            }
            else if (man.Contains("dell") || man.Contains("lenovo") || man.Contains("intel") || host.Contains("pc") || host.Contains("desktop") || host.Contains("laptop"))
            {
                device.DeviceType = "Computador / Estação de Trabalho";
                device.DeviceIcon = "💻";
            }
            else
            {
                device.DeviceType = "Dispositivo Conectado";
                device.DeviceIcon = "🔌";
            }
        }

        private static async Task<string> CheckCommonServicesAsync(string ip)
        {
            var openList = new List<string>();

            // Portas comuns para rápida inspeção
            int[] ports = new int[] { 80, 443, 445, 3389, 9100 };
            string[] names = new string[] { "Web (80)", "HTTPS (443)", "SMB (445)", "RDP (3389)", "Print (9100)" };

            for (int i = 0; i < ports.Length; i++)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(ip, ports[i]);
                        var completed = await Task.WhenAny(connectTask, Task.Delay(120));
                        if (completed == connectTask && client.Connected)
                        {
                            openList.Add(names[i]);
                        }
                    }
                }
                catch { }
            }

            if (openList.Count > 0)
            {
                return string.Join(" • ", openList.ToArray());
            }

            return "Sem portas de serviço públicas";
        }

        // ================= 4. AÇÕES DE 1-CLIQUE DO WINDOWS =================

        public static async Task<string> RunIpconfigReleaseRenewAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== INICIANDO RENOVAÇÃO DE IP (DHCP) ===");
                sb.AppendLine("1. Liberando concessão atual (ipconfig /release)...");

                string releaseOut = RunSystemProcess("ipconfig.exe", "/release");
                sb.AppendLine(releaseOut.Trim());

                sb.AppendLine();
                sb.AppendLine("2. Solicitando novo endereço IP ao roteador (ipconfig /renew)...");
                string renewOut = RunSystemProcess("ipconfig.exe", "/renew");
                sb.AppendLine(renewOut.Trim());

                sb.AppendLine();
                sb.AppendLine("=== PROCESSO DE RENOVAÇÃO CONCLUÍDO COM SUCESSO! ===");
                return sb.ToString();
            });
        }

        public static async Task<string> RunFlushDnsAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== LIMPEZA DE CACHE DNS (FLUSH DNS) ===");
                string flushOut = RunSystemProcess("ipconfig.exe", "/flushdns");
                sb.AppendLine(flushOut.Trim());
                sb.AppendLine("=== CACHE DO RESOLVEDOR DNS LIMPO COM SUCESSO! ===");
                return sb.ToString();
            });
        }

        public static async Task<string> RunResetTcpIpAsync()
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== REPARO DA PILHA TCP/IP E WINSOCK ===");
                sb.AppendLine("1. Redefinindo Catálogo Winsock (netsh winsock reset)...");
                string winsockOut = RunSystemProcess("netsh.exe", "winsock reset");
                sb.AppendLine(winsockOut.Trim());

                sb.AppendLine();
                sb.AppendLine("2. Redefinindo Protocolo TCP/IP (netsh int ip reset)...");
                string ipResetOut = RunSystemProcess("netsh.exe", "int ip reset");
                sb.AppendLine(ipResetOut.Trim());

                sb.AppendLine();
                sb.AppendLine("=== REPARO CONCLUÍDO! É RECOMENDADO REINICIAR O COMPUTADOR. ===");
                return sb.ToString();
            });
        }

        public static async Task<string> RestartActiveAdapterAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                    if (activeNic == null)
                    {
                        return "Nenhum adaptador ativo encontrado para reiniciar.";
                    }

                    string nicName = activeNic.Name;
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("=== REINICIANDO ADAPTADOR: {0} ===", nicName));

                    // Desativar
                    sb.AppendLine("1. Desativando interface de rede...");
                    RunSystemProcess("netsh.exe", string.Format("interface set interface \"{0}\" admin=disable", nicName));

                    Thread.Sleep(2000);

                    // Reativar
                    sb.AppendLine("2. Reativando interface de rede...");
                    RunSystemProcess("netsh.exe", string.Format("interface set interface \"{0}\" admin=enable", nicName));

                    sb.AppendLine("=== ADAPTADOR REINICIADO COM SUCESSO! ===");
                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return "Erro ao reiniciar adaptador: " + ex.Message;
                }
            });
        }

        private static string RunSystemProcess(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(15000);
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                return "Falha ao executar: " + ex.Message;
            }

            return "Comando concluído.";
        }

        // ================= HELPERS GERAIS DE REDE =================

        public static string GetDefaultGatewayIp()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;

                    var ipProps = nic.GetIPProperties();
                    foreach (var gw in ipProps.GatewayAddresses)
                    {
                        if (gw.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !gw.Address.ToString().StartsWith("0.0.0.0"))
                        {
                            return gw.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetLocalMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                if (nic != null)
                {
                    var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 6)
                    {
                        return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                            bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
                    }
                }
            }
            catch { }
            return "--";
        }

        public static NetworkInterfaceSummary GetActiveInterfaceSummary()
        {
            var summary = new NetworkInterfaceSummary();

            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                if (nic != null)
                {
                    summary.Name = nic.Name;
                    summary.Description = nic.Description;
                    summary.Status = "Conectado (" + nic.OperationalStatus.ToString() + ")";
                    summary.SpeedDisplay = nic.Speed >= 1000000000
                        ? string.Format("{0:F1} Gbps", nic.Speed / 1000000000.0)
                        : string.Format("{0:F0} Mbps", nic.Speed / 1000000.0);

                    var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 6)
                    {
                        summary.MacAddress = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                            bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
                    }

                    var ipProps = nic.GetIPProperties();
                    foreach (var u in ipProps.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            summary.Ipv4Address = u.Address.ToString();
                            if (u.IPv4Mask != null) summary.SubnetMask = u.IPv4Mask.ToString();
                            break;
                        }
                    }

                    var gw = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (gw != null) summary.GatewayAddress = gw.Address.ToString();

                    var dnsList = new List<string>();
                    foreach (var d in ipProps.DnsAddresses)
                    {
                        if (d.AddressFamily == AddressFamily.InterNetwork) dnsList.Add(d.ToString());
                    }
                    summary.DnsServers = dnsList.Count > 0 ? string.Join(", ", dnsList.ToArray()) : "--";

                    var dhcp = ipProps.DhcpServerAddresses.FirstOrDefault();
                    if (dhcp != null)
                    {
                        summary.DhcpServer = dhcp.ToString();
                        summary.IsDhcpEnabled = true;
                    }
                    else
                    {
                        summary.DhcpServer = "IP Estático / Manual";
                        summary.IsDhcpEnabled = false;
                    }
                }
            }
            catch { }

            return summary;
        }

        public static WifiConnectionDetails GetWifiDetails()
        {
            var wifi = new WifiConnectionDetails();

            try
            {
                // Verificar se há interface wireless ativa
                var wlanNic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

                if (wlanNic == null)
                {
                    wifi.HasWifiAdapter = false;
                    wifi.Ssid = "Sem Adaptador Wi-Fi instalado";
                    return wifi;
                }

                wifi.HasWifiAdapter = true;
                string output = RunSystemProcess("netsh.exe", "wlan show interfaces");

                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        int colIdx = trimmed.IndexOf(':');
                        if (colIdx <= 0 || colIdx >= trimmed.Length - 1) continue;

                        string key = trimmed.Substring(0, colIdx).Trim().ToLower();
                        string val = trimmed.Substring(colIdx + 1).Trim();

                        if (key.Equals("ssid"))
                        {
                            wifi.Ssid = val;
                            wifi.IsConnected = !string.IsNullOrEmpty(val);
                        }
                        else if (key.Equals("bssid"))
                        {
                            wifi.Bssid = val;
                        }
                        else if (key.Contains("sinal") || key.Contains("signal"))
                        {
                            wifi.SignalStrength = val;
                            int pct;
                            if (int.TryParse(val.Replace("%", "").Trim(), out pct))
                            {
                                wifi.SignalPercent = pct;
                            }
                        }
                        else if (key.Contains("canal") || key.Contains("channel"))
                        {
                            wifi.Channel = val;
                        }
                        else if (key.Contains("rádio") || key.Contains("radio"))
                        {
                            wifi.RadioType = val;
                        }
                        else if (key.Contains("recepção") || key.Contains("receive"))
                        {
                            wifi.ReceiveRate = val + " Mbps";
                        }
                        else if (key.Contains("transmissão") || key.Contains("transmit"))
                        {
                            wifi.TransmitRate = val + " Mbps";
                        }
                        else if (key.Contains("autenticação") || key.Contains("authentication"))
                        {
                            wifi.Authentication = val;
                        }
                        else if (key.Contains("cifra") || key.Contains("cipher"))
                        {
                            wifi.Cipher = val;
                        }
                    }
                }
            }
            catch { }

            return wifi;
        }
    }
}
