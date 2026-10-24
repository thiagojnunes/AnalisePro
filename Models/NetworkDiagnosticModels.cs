using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace AnalisePro.Models
{
    public class NetworkDeviceItem
    {
        public string IpAddress { get; set; }
        public string HostName { get; set; }
        public string MacAddress { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceType { get; set; }
        public string DeviceIcon { get; set; }
        public long LatencyMs { get; set; }
        public string LatencyDisplay { get; set; }
        public string OpenServices { get; set; }
        public bool IsLocalMachine { get; set; }
        public bool IsGateway { get; set; }
        public SolidColorBrush StatusBrush { get; set; }

        public NetworkDeviceItem()
        {
            HostName = "Resolvendo...";
            Manufacturer = "Desconhecido";
            DeviceType = "Dispositivo de Rede";
            DeviceIcon = "💻";
            LatencyDisplay = "--";
            OpenServices = "--";
        }
    }

    public class CableDiagnosticInfo
    {
        public string AdapterName { get; set; }
        public string AdapterDescription { get; set; }
        public string LinkSpeedDisplay { get; set; }
        public long LinkSpeedBps { get; set; }
        public bool IsGigabitCapable { get; set; }
        public bool IsConnected { get; set; }
        public bool HasCableWarning { get; set; }
        public string WarningTitle { get; set; }
        public string WarningDescription { get; set; }
        public string PinDiagnosticMessage { get; set; }
        public long PacketsReceivedErrors { get; set; }
        public long PacketsDiscarded { get; set; }
        public SolidColorBrush StatusBrush { get; set; }

        public CableDiagnosticInfo()
        {
            LinkSpeedDisplay = "Desconectado";
            WarningTitle = "";
            WarningDescription = "";
            PinDiagnosticMessage = "";
        }
    }

    public class ConnectivityCheckResult
    {
        public string TargetName { get; set; }
        public string TargetAddress { get; set; }
        public long? LatencyMs { get; set; }
        public string LatencyDisplay { get; set; }
        public string StatusDisplay { get; set; }
        public SolidColorBrush StatusBrush { get; set; }

        public ConnectivityCheckResult()
        {
            LatencyDisplay = "--";
            StatusDisplay = "Testando...";
        }
    }

    public class NetworkInterfaceSummary
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string MacAddress { get; set; }
        public string Ipv4Address { get; set; }
        public string SubnetMask { get; set; }
        public string GatewayAddress { get; set; }
        public string DnsServers { get; set; }
        public string DhcpServer { get; set; }
        public bool IsDhcpEnabled { get; set; }
        public string Status { get; set; }
        public string SpeedDisplay { get; set; }
    }
}
