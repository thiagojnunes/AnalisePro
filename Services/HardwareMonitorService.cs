using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using AnalisePro.Models;
using LibreHardwareMonitor.Hardware;

namespace AnalisePro.Services
{
    public class HardwareMonitorService : IDisposable
    {
        private readonly Computer _computer;
        private bool _isOpen = false;

        // Windows Win32 GetSystemTimes for 100% accurate CPU usage (Task Manager method)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out ComFileTime lpIdleTime, out ComFileTime lpKernelTime, out ComFileTime lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct ComFileTime
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public ulong ToUInt64() { return ((ulong)dwHighDateTime << 32) | dwLowDateTime; }
        }

        private ulong _prevIdleTime = 0;
        private ulong _prevKernelTime = 0;
        private ulong _prevUserTime = 0;
        private bool _hasPrevTimes = false;

        public HardwareMonitorService()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsBatteryEnabled = true
            };

            try
            {
                _computer.Open();
                _isOpen = true;
            }
            catch (Exception)
            {
                _isOpen = false;
            }

            // Prime the first CPU time sample
            CalculateSystemCpuUsage();
        }

        public SystemSnapshot GetSnapshot()
        {
            var snapshot = new SystemSnapshot
            {
                IsAdmin = AdminHelper.IsRunningAsAdmin(),
                OsName = GetFriendlyOsName(),
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue),
                Timestamp = DateTime.Now
            };

            if (_isOpen)
            {
                UpdateAllHardware(_computer);
                ProcessHardwareData(snapshot);
            }
            else
            {
                FallbackSystemMetrics(snapshot);
            }

            // Always use Win32 GetSystemTimes as the gold standard for CPU usage
            double win32Cpu = CalculateSystemCpuUsage();
            if (win32Cpu > 0 || snapshot.Cpu.TotalLoad == 0)
            {
                snapshot.Cpu.TotalLoad = win32Cpu;
            }

            // Fallback for CPU temperature if LibreHardwareMonitor didn't detect MSR
            if (!snapshot.Cpu.PackageTemp.HasValue)
            {
                double? wmiTemp = GetWmiAcpiTemperature();
                if (wmiTemp.HasValue)
                {
                    snapshot.Cpu.PackageTemp = Math.Round(wmiTemp.Value, 0);
                    if (!snapshot.Cpu.AverageTemp.HasValue)
                        snapshot.Cpu.AverageTemp = Math.Round(wmiTemp.Value, 0);
                    if (!snapshot.Cpu.MaxCoreTemp.HasValue)
                        snapshot.Cpu.MaxCoreTemp = Math.Round(wmiTemp.Value, 0);
                }
            }

            // Fallback for GPU if empty or missing temperature
            EnrichGpuInfo(snapshot);

            // Enrich Disk data with standard DriveInfo & Physical model
            EnrichDriveInfo(snapshot);

            // Preencher especificações estáticas detalhadas de hardware
            HardwareInfoService.PopulateStaticHardwareSpecs(snapshot);

            return snapshot;
        }

        private double CalculateSystemCpuUsage()
        {
            ComFileTime idleTime, kernelTime, userTime;
            if (!GetSystemTimes(out idleTime, out kernelTime, out userTime))
                return 0;

            ulong curIdle = idleTime.ToUInt64();
            ulong curKernel = kernelTime.ToUInt64();
            ulong curUser = userTime.ToUInt64();

            if (!_hasPrevTimes)
            {
                _prevIdleTime = curIdle;
                _prevKernelTime = curKernel;
                _prevUserTime = curUser;
                _hasPrevTimes = true;
                return 0;
            }

            ulong usrDiff = curUser - _prevUserTime;
            ulong kerDiff = curKernel - _prevKernelTime;
            ulong idlDiff = curIdle - _prevIdleTime;

            _prevIdleTime = curIdle;
            _prevKernelTime = curKernel;
            _prevUserTime = curUser;

            ulong sysDiff = usrDiff + kerDiff;
            if (sysDiff == 0) return 0;

            double cpuUsage = (double)(sysDiff - idlDiff) * 100.0 / sysDiff;
            if (cpuUsage < 0) cpuUsage = 0;
            if (cpuUsage > 100) cpuUsage = 100;

            return Math.Round(cpuUsage, 1);
        }

        private void UpdateAllHardware(Computer computer)
        {
            foreach (var hardware in computer.Hardware)
            {
                try
                {
                    hardware.Update();
                    foreach (var sub in hardware.SubHardware)
                    {
                        sub.Update();
                    }
                }
                catch { }
            }
        }

        private void ProcessHardwareData(SystemSnapshot snapshot)
        {
            foreach (var hw in _computer.Hardware)
            {
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        ProcessCpu(hw, snapshot.Cpu);
                        break;

                    case HardwareType.Memory:
                        ProcessMemory(hw, snapshot.Memory);
                        break;

                    case HardwareType.GpuIntel:
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                        var gpu = new GpuInfo { Name = hw.Name };
                        ProcessGpu(hw, gpu);
                        snapshot.Gpus.Add(gpu);
                        break;

                    case HardwareType.Storage:
                        var disk = new DiskInfo { Name = hw.Name, PhysicalModel = hw.Name };
                        ProcessStorage(hw, disk);
                        snapshot.Disks.Add(disk);
                        break;

                    case HardwareType.Network:
                        var net = new NetworkInfo { Name = hw.Name };
                        ProcessNetwork(hw, net);
                        if (net.DownloadSpeedKbps > 0 || net.UploadSpeedKbps > 0 || net.TotalDownloadMb > 0)
                        {
                            snapshot.Networks.Add(net);
                        }
                        break;

                    case HardwareType.Motherboard:
                        snapshot.MotherboardName = hw.Name;
                        break;

                    case HardwareType.Battery:
                        ProcessBattery(hw, snapshot.Battery);
                        break;
                }
            }

            if (snapshot.Cpu.CoreCount == 0)
            {
                snapshot.Cpu.CoreCount = Environment.ProcessorCount;
                snapshot.Cpu.ThreadCount = Environment.ProcessorCount;
            }
        }

        private void ProcessCpu(IHardware hw, CpuInfo cpu)
        {
            cpu.Name = hw.Name;
            var temps = new List<double>();
            var coreLoads = new Dictionary<string, CpuCoreInfo>();

            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        temps.Add(val);
                        if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                            cpu.PackageTemp = val;
                        else if (sensor.Name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0)
                            cpu.MaxCoreTemp = val;
                        else if (sensor.Name.IndexOf("Core Average", StringComparison.OrdinalIgnoreCase) >= 0)
                            cpu.AverageTemp = val;
                        break;

                    case SensorType.Load:
                        if (sensor.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                            cpu.TotalLoad = val;
                        else if (sensor.Name.StartsWith("CPU Core", StringComparison.OrdinalIgnoreCase))
                        {
                            string coreName = sensor.Name;
                            int threadIdx = coreName.IndexOf(" Thread", StringComparison.OrdinalIgnoreCase);
                            if (threadIdx > 0) coreName = coreName.Substring(0, threadIdx);

                            if (!coreLoads.ContainsKey(coreName))
                            {
                                coreLoads[coreName] = new CpuCoreInfo { Name = coreName, Load = val };
                            }
                            else
                            {
                                coreLoads[coreName].Load = (coreLoads[coreName].Load + val) / 2.0;
                            }
                        }
                        break;

                    case SensorType.Power:
                        if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                            cpu.PowerWatts = val;
                        break;

                    case SensorType.Clock:
                        if (sensor.Name.IndexOf("Core #1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            sensor.Name.IndexOf("Bus Speed", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            if (!cpu.ClockMhz.HasValue || val > cpu.ClockMhz.Value)
                                cpu.ClockMhz = val;
                        }
                        break;
                }
            }

            if (temps.Count > 0)
            {
                if (!cpu.PackageTemp.HasValue) cpu.PackageTemp = temps.Max();
                if (!cpu.MaxCoreTemp.HasValue) cpu.MaxCoreTemp = temps.Max();
                cpu.AverageTemp = temps.Average();
            }

            cpu.Cores = coreLoads.Values.OrderBy(c => c.Name).ToList();
        }

        private void ProcessMemory(IHardware hw, MemoryInfo mem)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                if (hw.Name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (sensor.Name.Equals("Memory Used", StringComparison.OrdinalIgnoreCase))
                        mem.VirtualUsedGb = val;
                    else if (sensor.Name.Equals("Memory Available", StringComparison.OrdinalIgnoreCase))
                        mem.VirtualTotalGb = mem.VirtualUsedGb + val;
                    else if (sensor.SensorType == SensorType.Load)
                        mem.VirtualUsagePercent = val;
                }
                else
                {
                    if (sensor.Name.Equals("Memory Used", StringComparison.OrdinalIgnoreCase))
                        mem.UsedGb = val;
                    else if (sensor.Name.Equals("Memory Available", StringComparison.OrdinalIgnoreCase))
                        mem.AvailableGb = val;
                    else if (sensor.SensorType == SensorType.Load)
                        mem.UsagePercent = val;
                }
            }

            if (mem.TotalGb == 0 && (mem.UsedGb > 0 || mem.AvailableGb > 0))
            {
                mem.TotalGb = mem.UsedGb + mem.AvailableGb;
            }
        }

        private void ProcessGpu(IHardware hw, GpuInfo gpu)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (sensor.Name.IndexOf("Hot", StringComparison.OrdinalIgnoreCase) >= 0)
                            gpu.HotspotTemp = val;
                        else
                            gpu.Temperature = val;
                        break;

                    case SensorType.Load:
                        if (sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            sensor.Name.IndexOf("D3D 3D", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!gpu.CoreLoad.HasValue || val > gpu.CoreLoad.Value)
                                gpu.CoreLoad = val;
                        }
                        else if (sensor.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            gpu.MemoryLoad = val;
                        }
                        break;

                    case SensorType.Clock:
                        if (sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                            gpu.CoreClockMhz = val;
                        break;

                    case SensorType.Power:
                        gpu.PowerWatts = val;
                        break;

                    case SensorType.SmallData:
                        if (sensor.Name.IndexOf("Total", StringComparison.OrdinalIgnoreCase) >= 0)
                            gpu.VramTotalMb = val;
                        else if (sensor.Name.IndexOf("Used", StringComparison.OrdinalIgnoreCase) >= 0)
                            gpu.VramUsedMb = val;
                        break;
                }
            }

            if (gpu.VramTotalMb.HasValue && gpu.VramTotalMb.Value > 0 && gpu.VramUsedMb.HasValue)
            {
                gpu.VramUsagePercent = (gpu.VramUsedMb.Value / gpu.VramTotalMb.Value) * 100.0;
            }
        }

        private void EnrichGpuInfo(SystemSnapshot snapshot)
        {
            if (snapshot.Gpus.Count == 0)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            string name = obj["Name"] != null ? obj["Name"].ToString() : "Placa de Vídeo";
                            double vramMb = 0;
                            if (obj["AdapterRAM"] != null)
                            {
                                vramMb = Convert.ToDouble(obj["AdapterRAM"]) / (1024.0 * 1024.0);
                            }

                            snapshot.Gpus.Add(new GpuInfo
                            {
                                Name = name,
                                VramTotalMb = Math.Round(vramMb, 0)
                            });
                        }
                    }
                }
                catch { }
            }

            foreach (var gpu in snapshot.Gpus)
            {
                if (!gpu.Temperature.HasValue)
                {
                    bool isIntegrated = gpu.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        gpu.Name.IndexOf("UHD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        gpu.Name.IndexOf("Iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        gpu.Name.IndexOf("Vega", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        gpu.Name.IndexOf("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isIntegrated || snapshot.Gpus.Count == 1)
                    {
                        gpu.IsIntegrated = true;
                        if (snapshot.Cpu.PackageTemp.HasValue)
                        {
                            gpu.Temperature = snapshot.Cpu.PackageTemp.Value;
                        }
                    }
                }
            }
        }

        private double? GetWmiAcpiTemperature()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["Temperature"] != null)
                        {
                            double kelvin = Convert.ToDouble(obj["Temperature"]);
                            if (kelvin > 220 && kelvin < 450)
                            {
                                return kelvin - 273.15;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["CurrentTemperature"] != null)
                        {
                            double dKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                            double celsius = (dKelvin - 2732.0) / 10.0;
                            if (celsius > 10 && celsius < 120)
                            {
                                return celsius;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private void ProcessStorage(IHardware hw, DiskInfo disk)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                if (sensor.SensorType == SensorType.Temperature)
                    disk.Temperature = val;
                else if (sensor.SensorType == SensorType.Throughput)
                {
                    if (sensor.Name.IndexOf("Read", StringComparison.OrdinalIgnoreCase) >= 0)
                        disk.ReadSpeedKbps = val / 1024.0;
                    else if (sensor.Name.IndexOf("Write", StringComparison.OrdinalIgnoreCase) >= 0)
                        disk.WriteSpeedKbps = val / 1024.0;
                }
                else if (sensor.SensorType == SensorType.Load)
                    disk.UsagePercent = val;
            }
        }

        private void ProcessNetwork(IHardware hw, NetworkInfo net)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                if (sensor.SensorType == SensorType.Throughput)
                {
                    if (sensor.Name.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0)
                        net.DownloadSpeedKbps = val / 1024.0;
                    else if (sensor.Name.IndexOf("Upload", StringComparison.OrdinalIgnoreCase) >= 0)
                        net.UploadSpeedKbps = val / 1024.0;
                }
                else if (sensor.SensorType == SensorType.Data)
                {
                    if (sensor.Name.IndexOf("Downloaded", StringComparison.OrdinalIgnoreCase) >= 0)
                        net.TotalDownloadMb = val * 1024.0;
                    else if (sensor.Name.IndexOf("Uploaded", StringComparison.OrdinalIgnoreCase) >= 0)
                        net.TotalUploadMb = val * 1024.0;
                }
            }
        }

        private void ProcessBattery(IHardware hw, BatteryInfo bat)
        {
            bat.HasBattery = true;
            foreach (var sensor in hw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                double val = sensor.Value.Value;

                if (sensor.SensorType == SensorType.Level)
                {
                    bat.Percent = val;
                }
            }
            bat.StatusDescription = string.Format("{0:F0}% disponível", bat.Percent);
        }

        private void EnrichDriveInfo(SystemSnapshot snapshot)
        {
            try
            {
                var systemDrives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    .ToList();

                // Get physical disk models
                var physicalModels = new List<string>();
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_DiskDrive"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            if (obj["Model"] != null) physicalModels.Add(obj["Model"].ToString().Trim());
                        }
                    }
                }
                catch { }

                string defaultModel = physicalModels.Count > 0 ? physicalModels[0] : "NVMe / SSD";

                if (snapshot.Disks.Count == 0)
                {
                    for (int i = 0; i < systemDrives.Count; i++)
                    {
                        var d = systemDrives[i];
                        double totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                        double freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        double usedGb = totalGb - freeGb;
                        double percent = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0;

                        string model = i < physicalModels.Count ? physicalModels[i] : defaultModel;

                        snapshot.Disks.Add(new DiskInfo
                        {
                            Name = string.Format("{0} ({1})", d.Name, d.VolumeLabel),
                            PhysicalModel = model,
                            VolumeLabel = d.VolumeLabel,
                            DriveFormat = d.DriveFormat,
                            TotalGb = Math.Round(totalGb, 1),
                            UsedGb = Math.Round(usedGb, 1),
                            FreeGb = Math.Round(freeGb, 1),
                            UsagePercent = Math.Round(percent, 1)
                        });
                    }
                }
                else
                {
                    for (int i = 0; i < snapshot.Disks.Count && i < systemDrives.Count; i++)
                    {
                        var d = systemDrives[i];
                        var disk = snapshot.Disks[i];

                        if (string.IsNullOrEmpty(disk.PhysicalModel))
                        {
                            disk.PhysicalModel = i < physicalModels.Count ? physicalModels[i] : defaultModel;
                        }

                        if (disk.TotalGb == 0)
                        {
                            double totalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                            double freeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                            double usedGb = totalGb - freeGb;
                            double percent = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0;

                            disk.VolumeLabel = string.Format("{0} {1}", d.Name, d.VolumeLabel).Trim();
                            disk.DriveFormat = d.DriveFormat;
                            disk.TotalGb = Math.Round(totalGb, 1);
                            disk.UsedGb = Math.Round(usedGb, 1);
                            disk.FreeGb = Math.Round(freeGb, 1);
                            disk.UsagePercent = Math.Round(percent, 1);
                        }
                    }
                }
            }
            catch { }
        }

        private void FallbackSystemMetrics(SystemSnapshot snapshot)
        {
            snapshot.Cpu.Name = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Processador do Sistema";
            snapshot.Cpu.CoreCount = Environment.ProcessorCount;
            snapshot.Cpu.ThreadCount = Environment.ProcessorCount;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        ulong totalBytes = Convert.ToUInt64(obj["TotalVisibleMemorySize"]) * 1024;
                        ulong freeBytes = Convert.ToUInt64(obj["FreePhysicalMemory"]) * 1024;
                        ulong usedBytes = totalBytes - freeBytes;

                        snapshot.Memory.TotalGb = Math.Round(totalBytes / (1024.0 * 1024 * 1024), 2);
                        snapshot.Memory.UsedGb = Math.Round(usedBytes / (1024.0 * 1024 * 1024), 2);
                        snapshot.Memory.AvailableGb = Math.Round(freeBytes / (1024.0 * 1024 * 1024), 2);
                        snapshot.Memory.UsagePercent = Math.Round((double)usedBytes / totalBytes * 100.0, 1);
                    }
                }
            }
            catch { }
        }

        private string GetFriendlyOsName()
        {
            try
            {
                return Environment.OSVersion.VersionString;
            }
            catch
            {
                return "Windows";
            }
        }

        public void Dispose()
        {
            if (_isOpen)
            {
                try
                {
                    _computer.Close();
                }
                catch { }
                _isOpen = false;
            }
        }
    }
}
