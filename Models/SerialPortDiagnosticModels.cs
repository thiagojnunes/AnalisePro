using System;
using System.Windows.Media;

namespace AnalisePro.Models
{
    public class SerialPortInfoItem
    {
        public string PortName { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
        public string HardwareId { get; set; }
        public string Status { get; set; }
        public bool IsPciCard { get; set; }
        public string CardTypeDisplay { get; set; }
        public SolidColorBrush StatusBrush { get; set; }

        public SerialPortInfoItem()
        {
            Description = "Porta Serial Padrão";
            Manufacturer = "Desconhecido";
            Status = "OK";
            CardTypeDisplay = "Porta COM do Sistema";
        }
    }

    public class SerialTestDataEvent
    {
        public DateTime Timestamp { get; set; }
        public bool IsIncoming { get; set; }
        public string TextData { get; set; }
        public string HexData { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
    }
}
