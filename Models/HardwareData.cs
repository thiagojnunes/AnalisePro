using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace AnalisePro.Models
{
    public class CpuCoreInfo
    {
        public string Name { get; set; }
        public double Load { get; set; }
        public double? Temperature { get; set; }
        public double? ClockMhz { get; set; }
    }

    public class CpuInfo
    {
        public CpuInfo()
        {
            Cores = new List<CpuCoreInfo>();
        }

        public string Name { get; set; }
        public double TotalLoad { get; set; }
        public double? PackageTemp { get; set; }
        public double? MaxCoreTemp { get; set; }
        public double? AverageTemp { get; set; }
        public double? PowerWatts { get; set; }
        public double? ClockMhz { get; set; }
        public int CoreCount { get; set; }
        public int ThreadCount { get; set; }
        public List<CpuCoreInfo> Cores { get; set; }
    }

    public class MemoryInfo
    {
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double AvailableGb { get; set; }
        public double UsagePercent { get; set; }

        public double VirtualTotalGb { get; set; }
        public double VirtualUsedGb { get; set; }
        public double VirtualUsagePercent { get; set; }
    }

    public class GpuInfo
    {
        public string Name { get; set; }
        public double? CoreLoad { get; set; }
        public double? MemoryLoad { get; set; }
        public double? Temperature { get; set; }
        public double? HotspotTemp { get; set; }
        public double? CoreClockMhz { get; set; }
        public double? PowerWatts { get; set; }
        public double? VramUsedMb { get; set; }
        public double? VramTotalMb { get; set; }
        public double? VramUsagePercent { get; set; }
        public bool IsIntegrated { get; set; }
    }

    public class DiskInfo
    {
        public string Name { get; set; }
        public string PhysicalModel { get; set; }
        public string VolumeLabel { get; set; }
        public string DriveFormat { get; set; }
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double FreeGb { get; set; }
        public double UsagePercent { get; set; }
        public double? Temperature { get; set; }
        public double ReadSpeedKbps { get; set; }
        public double WriteSpeedKbps { get; set; }
    }

    public class NetworkInfo
    {
        public string Name { get; set; }
        public double DownloadSpeedKbps { get; set; }
        public double UploadSpeedKbps { get; set; }
        public double TotalDownloadMb { get; set; }
        public double TotalUploadMb { get; set; }
    }

    public class BatteryInfo
    {
        public bool HasBattery { get; set; }
        public double Percent { get; set; }
        public bool IsCharging { get; set; }
        public string StatusDescription { get; set; }
    }

    // ================= MODELOS DE ESPECIFICAÇÕES DE HARDWARE =================

    public class CpuSpecDetails
    {
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Codename { get; set; }
        public string Socket { get; set; }
        public string Description { get; set; }
        public int Cores { get; set; }
        public int Threads { get; set; }
        public double BaseClockMhz { get; set; }
        public string L1Cache { get; set; }
        public string L2Cache { get; set; }
        public string L3Cache { get; set; }
        public string Instructions { get; set; }
    }

    public class MotherboardDetails
    {
        public string Manufacturer { get; set; }
        public string Product { get; set; }
        public string Version { get; set; }
        public string SerialNumber { get; set; }
        public string BiosVendor { get; set; }
        public string BiosVersion { get; set; }
        public string BiosReleaseDate { get; set; }
    }

    public class RamModuleDetails
    {
        public string Slot { get; set; }
        public string Manufacturer { get; set; }
        public string PartNumber { get; set; }
        public double CapacityGb { get; set; }
        public int SpeedMt { get; set; }
        public int ConfiguredSpeedMt { get; set; }
        public string MemoryType { get; set; }
    }

    public class GpuSpecDetails
    {
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string DriverVersion { get; set; }
        public string VideoProcessor { get; set; }
        public double AdapterRamGb { get; set; }
        public string Resolution { get; set; }
    }

    public class PeripheralItem
    {
        public string Category { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Status { get; set; }
    }

    // ================= DISK ANALYZER TREE & PIE ITEM =================

    public class DiskDirectoryItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay { get; set; }
        public double Percentage { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
    }

    // ================= FULL SYSTEM SNAPSHOT =================

    public class SystemSnapshot
    {
        public SystemSnapshot()
        {
            Timestamp = DateTime.Now;
            Cpu = new CpuInfo();
            Memory = new MemoryInfo();
            Gpus = new List<GpuInfo>();
            Disks = new List<DiskInfo>();
            Networks = new List<NetworkInfo>();
            Battery = new BatteryInfo();

            CpuSpecs = new CpuSpecDetails();
            Motherboard = new MotherboardDetails();
            RamModules = new List<RamModuleDetails>();
            GpuSpecs = new List<GpuSpecDetails>();
            Peripherals = new List<PeripheralItem>();
        }

        public bool IsAdmin { get; set; }
        public string MotherboardName { get; set; }
        public string OsName { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime Timestamp { get; set; }

        public CpuInfo Cpu { get; set; }
        public MemoryInfo Memory { get; set; }
        public List<GpuInfo> Gpus { get; set; }
        public List<DiskInfo> Disks { get; set; }
        public List<NetworkInfo> Networks { get; set; }
        public BatteryInfo Battery { get; set; }

        // Especificações detalhadas de hardware
        public CpuSpecDetails CpuSpecs { get; set; }
        public MotherboardDetails Motherboard { get; set; }
        public List<RamModuleDetails> RamModules { get; set; }
        public List<GpuSpecDetails> GpuSpecs { get; set; }
        public List<PeripheralItem> Peripherals { get; set; }
    }
}
