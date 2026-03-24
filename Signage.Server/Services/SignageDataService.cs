using Signage.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Signage.Server.Services;

/// <summary>
/// 電子看板數據服務（簡化版：使用內存存儲，實際應改用資料庫）
/// </summary>
public class SignageDataService
{
    private const string VisitorCentersCategory = "visitor-centers";
    private const string SignagePagesCategory = "signage-pages";
    private const string MediaLibraryCategory = "media-library";
    private const string DeviceStatusesCategory = "device-statuses";
    private const string PushCommandsCategory = "push-commands";
    private readonly SqliteJsonStore _store;

    // 模擬數據存儲
    private List<VisitorCenter> _visitorCenters = new();
    private List<SignagePage> _signagePages = new();
    private List<MediaContent> _mediaLibrary = new();
    private List<DeviceStatus> _deviceStatuses = new();
    private List<SignagePushCommand> _pushCommands = new();

    public SignageDataService(SqliteJsonStore store)
    {
        _store = store;
        LoadFromStore();

        if (!_visitorCenters.Any() && !_signagePages.Any() && !_mediaLibrary.Any() && !_deviceStatuses.Any() && !_pushCommands.Any())
        {
            InitializeSampleData();
            PersistAll();
        }
    }

    // ============= 遊客中心管理 =============

    /// <summary>
    /// 取得所有遊客中心
    /// </summary>
    public List<VisitorCenter> GetAllVisitorCenters()
    {
        NormalizeCenterDevices();
        return _visitorCenters.ToList();
    }

    /// <summary>
    /// 取得單一遊客中心
    /// </summary>
    public VisitorCenter? GetVisitorCenter(string centerId)
    {
        NormalizeCenterDevices();
        return _visitorCenters.FirstOrDefault(c => c.Id == centerId);
    }

    /// <summary>
    /// 建立新遊客中心
    /// </summary>
    public VisitorCenter CreateVisitorCenter(string name, string location)
    {
        var center = new VisitorCenter
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Location = location,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now
        };
        _visitorCenters.Add(center);
        PersistVisitorCenters();
        return center;
    }

    /// <summary>
    /// 更新遊客中心
    /// </summary>
    public bool UpdateVisitorCenter(string centerId, string name, string location, string description = "")
    {
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null) return false;

        center.Name = name;
        center.Location = location;
        center.Description = description;
        center.LastModified = DateTime.Now;
        PersistVisitorCenters();
        return true;
    }

    /// <summary>
    /// 刪除遊客中心
    /// </summary>
    public bool DeleteVisitorCenter(string centerId)
    {
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null) return false;

        _visitorCenters.Remove(center);
    PersistVisitorCenters();
        return true;
    }

    // ============= 版型配置管理 =============

    /// <summary>
    /// 取得全部版型配置（全域版型庫）
    /// </summary>
    public List<SignagePage> GetAllSignagePages()
    {
        return _signagePages
            .Select(CloneAndNormalizePage)
            .OrderByDescending(p => p.LastModified)
            .ToList();
    }

    /// <summary>
    /// 取得指定中心的所有版型配置
    /// </summary>
    public List<SignagePage> GetPagesByCenter(string centerId)
    {
        return _signagePages
            .Where(p => p.VisitorCenterId == centerId)
            .Select(CloneAndNormalizePage)
            .ToList();
    }

    /// <summary>
    /// 取得單一版型配置
    /// </summary>
    public SignagePage? GetSignagePage(string pageId)
    {
        var page = _signagePages.FirstOrDefault(p => p.Id == pageId);
        return page == null ? null : CloneAndNormalizePage(page);
    }

    /// <summary>
    /// 取得指定中心目前啟用的版型配置
    /// </summary>
    public SignagePage? GetActivePageByCenter(string centerId)
    {
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null || string.IsNullOrWhiteSpace(center.ActivePageId))
            return null;

        var page = _signagePages.FirstOrDefault(p => p.Id == center.ActivePageId && p.VisitorCenterId == centerId);
        return page == null ? null : CloneAndNormalizePage(page);
    }

    /// <summary>
    /// 根據設備 ID 取得該設備的播放版型
    /// </summary>
    public SignagePage? GetPageByDevice(string deviceId)
    {
        NormalizeCenterDevices();

        // 找所有中心，尋找包含該 deviceId 的設備配置
        var device = _visitorCenters
            .SelectMany(c => c.Devices, (center, dev) => new { Center = center, Device = dev })
            .FirstOrDefault(x => x.Device.DeviceId == deviceId);

        if (device?.Device?.AssignedPageId == null)
            return null;

        var page = _signagePages.FirstOrDefault(p => p.Id == device.Device.AssignedPageId);
        return page == null ? null : CloneAndNormalizePage(page);
    }

    /// <summary>
    /// 根據中心 ID 和設備編號取得該設備的播放版型
    /// </summary>
    public SignagePage? GetPageByDeviceNumber(string centerId, int deviceNumber)
    {
        NormalizeCenterDevices();

        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null)
            return null;

        var device = center.Devices?.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
        if (device?.AssignedPageId == null)
            return null;

        var page = _signagePages.FirstOrDefault(p => p.Id == device.AssignedPageId);
        return page == null ? null : CloneAndNormalizePage(page);
    }

    /// <summary>
    /// 根據設備公開代碼取得播放版型
    /// </summary>
    public SignagePage? GetPageByDeviceSlug(string deviceSlug)
    {
        NormalizeCenterDevices();

        if (string.IsNullOrWhiteSpace(deviceSlug))
            return null;

        var device = _visitorCenters
            .SelectMany(c => c.Devices)
            .FirstOrDefault(d => string.Equals(d.DeviceSlug, deviceSlug, StringComparison.OrdinalIgnoreCase));

        if (device == null || string.IsNullOrWhiteSpace(device.AssignedPageId))
            return null;

        var page = _signagePages.FirstOrDefault(p => p.Id == device.AssignedPageId);
        return page == null ? null : CloneAndNormalizePage(page);
    }

    /// <summary>
    /// 取得中心底下的設備節點（中心 > 設備）
    /// </summary>
    public List<DeviceHierarchyNode> GetDeviceHierarchyByCenter(string centerId)
    {
        NormalizeCenterDevices();

        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null)
            return new();

        return center.Devices
            .OrderBy(d => d.DeviceNumber)
            .Select(d =>
            {
                var status = _deviceStatuses.FirstOrDefault(s => s.DeviceId == d.DeviceId);
                var pageName = _signagePages.FirstOrDefault(p => p.Id == d.AssignedPageId)?.Name ?? "未套用";

                return new DeviceHierarchyNode
                {
                    CenterId = center.Id,
                    CenterName = center.Name,
                    DeviceNumber = d.DeviceNumber,
                    DeviceId = d.DeviceId,
                    DeviceSlug = d.DeviceSlug,
                    DeviceName = string.IsNullOrWhiteSpace(d.Name) ? $"設備 {d.DeviceNumber}" : d.Name,
                    AssignedPageId = d.AssignedPageId,
                    AssignedPageName = pageName,
                    ScreenOrientation = d.ScreenOrientation,
                    IsActive = d.IsActive,
                    IsOnline = status?.IsOnline ?? false,
                    LastSeen = status?.LastSeen ?? DateTime.MinValue
                };
            })
            .ToList();
    }

    /// <summary>
    /// 更新設備設定（名稱、版型、方向、啟用與 slug）
    /// </summary>
    public bool UpdateDeviceConfig(string centerId, int deviceNumber, string? name, string? assignedPageId, ScreenOrientation? orientation, bool? isActive, string? deviceSlug)
    {
        NormalizeCenterDevices();

        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null)
            return false;

        var device = center.Devices.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
        if (device == null)
            return false;

        if (!string.IsNullOrWhiteSpace(name))
            device.Name = name.Trim();

        if (assignedPageId != null)
            device.AssignedPageId = assignedPageId.Trim();

        if (orientation.HasValue)
            device.ScreenOrientation = orientation.Value;

        if (isActive.HasValue)
            device.IsActive = isActive.Value;

        if (!string.IsNullOrWhiteSpace(deviceSlug))
        {
            var normalizedSlug = NormalizeSlug(deviceSlug);
            var conflict = _visitorCenters
                .SelectMany(c => c.Devices)
                .Any(d => d.DeviceId != device.DeviceId && string.Equals(d.DeviceSlug, normalizedSlug, StringComparison.OrdinalIgnoreCase));

            if (conflict)
                return false;

            device.DeviceSlug = normalizedSlug;
        }

        device.LastModified = DateTime.Now;
        center.LastModified = DateTime.Now;
        PersistVisitorCenters();
        return true;
    }

    /// <summary>
    /// 建立新版型配置
    /// </summary>
    public SignagePage CreateSignagePage(string centerId, string name, LayoutType layout)
    {
        var isBoundToCenter = !string.IsNullOrWhiteSpace(centerId);
        var isFirstPageForCenter = isBoundToCenter && !_signagePages.Any(p => p.VisitorCenterId == centerId);

        var page = new SignagePage
        {
            Id = Guid.NewGuid().ToString(),
            VisitorCenterId = centerId,
            Name = name,
            Layout = layout,
            ZoneCount = 3,
            ScreenOrientation = ScreenOrientation.Landscape16x9,
            MarqueePosition = MarqueeVerticalPosition.Top,
            MarqueeVerticalPercent = 10,
            Regions = CreateRegionsFromPreset(33, 33, 34),
            IsActive = isFirstPageForCenter,
            LastModified = DateTime.Now
        };

        _signagePages.Add(page);

        if (isFirstPageForCenter)
        {
            var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
            if (center != null)
            {
                center.ActivePageId = page.Id;
                center.LastModified = DateTime.Now;
                PersistVisitorCenters();
            }
        }

        PersistSignagePages();
        return page;
    }

    /// <summary>
    /// 更新版型配置
    /// </summary>
    public bool UpdateSignagePage(string pageId, SignagePage updatedPage)
    {
        var page = _signagePages.FirstOrDefault(p => p.Id == pageId);
        if (page == null) return false;

        page.Name = updatedPage.Name;
        page.Layout = updatedPage.Layout;
        page.ZoneCount = Math.Clamp(updatedPage.ZoneCount, 1, 3);
        page.ScreenOrientation = updatedPage.ScreenOrientation;
        page.MarqueePosition = updatedPage.MarqueePosition;
        page.MarqueeVerticalPercent = Math.Clamp(updatedPage.MarqueeVerticalPercent, 0, 100);
        page.TopContent = updatedPage.TopContent;
        page.MiddleContentUrl = updatedPage.MiddleContentUrl;
        page.BottomContentUrl = updatedPage.BottomContentUrl;
        page.LeftContent = updatedPage.LeftContent;
        page.RightContent = updatedPage.RightContent;
        page.Regions = NormalizeRegions(updatedPage).Select(CloneRegion).ToList();
        page.IsActive = updatedPage.IsActive;
        page.LastModified = DateTime.Now;
        PersistSignagePages();
        return true;
    }

    /// <summary>
    /// 刪除版型配置
    /// </summary>
    public bool DeleteSignagePage(string pageId)
    {
        var page = _signagePages.FirstOrDefault(p => p.Id == pageId);
        if (page == null) return false;

        _signagePages.Remove(page);

        var center = _visitorCenters.FirstOrDefault(c => c.Id == page.VisitorCenterId);
        if (center != null && center.ActivePageId == pageId)
        {
            var replacementPage = _signagePages.FirstOrDefault(p => p.VisitorCenterId == page.VisitorCenterId);
            center.ActivePageId = replacementPage?.Id ?? "";
            center.LastModified = DateTime.Now;

            if (replacementPage != null)
            {
                replacementPage.IsActive = true;
                replacementPage.LastModified = DateTime.Now;
            }

            PersistVisitorCenters();
        }

        PersistSignagePages();
        return true;
    }

    /// <summary>
    /// 啟用版型配置
    /// </summary>
    public bool ActivatePage(string centerId, string pageId)
    {
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null) return false;

        var page = _signagePages.FirstOrDefault(p => p.Id == pageId && p.VisitorCenterId == centerId);
        if (page == null) return false;

        foreach (var pageItem in _signagePages.Where(p => p.VisitorCenterId == centerId))
        {
            pageItem.IsActive = pageItem.Id == pageId;
            pageItem.LastModified = DateTime.Now;
        }

        center.ActivePageId = pageId;
        center.LastModified = DateTime.Now;
        PersistVisitorCenters();
        PersistSignagePages();
        return true;
    }

    // ============= 媒體庫管理 =============

    /// <summary>
    /// 取得指定中心的媒體庫
    /// </summary>
    public List<MediaContent> GetMediaByCenter(string centerId)
    {
        return _mediaLibrary.Where(m => m.VisitorCenterId == centerId).ToList();
    }

    /// <summary>
    /// 上傳媒體素材
    /// </summary>
    public MediaContent AddMedia(string centerId, string title, string url, string mediaType, int duration = 10)
    {
        var media = new MediaContent
        {
            Id = Guid.NewGuid().ToString(),
            VisitorCenterId = centerId,
            Title = title,
            Url = url,
            MediaType = mediaType,
            Duration = duration,
            UploadedAt = DateTime.Now
        };
        _mediaLibrary.Add(media);
        PersistMediaLibrary();
        return media;
    }

    /// <summary>
    /// 刪除媒體素材
    /// </summary>
    public bool DeleteMedia(string mediaId)
    {
        var media = _mediaLibrary.FirstOrDefault(m => m.Id == mediaId);
        if (media == null) return false;

        _mediaLibrary.Remove(media);
    PersistMediaLibrary();
        return true;
    }

    // ============= 設備狀態管理 =============

    /// <summary>
    /// 取得所有設備狀態
    /// </summary>
    public List<DeviceStatus> GetAllDevices()
    {
        return _deviceStatuses.ToList();
    }

    /// <summary>
    /// 取得指定中心的所有設備
    /// </summary>
    public List<DeviceStatus> GetDevicesByCenter(string centerId)
    {
        return _deviceStatuses.Where(d => d.VisitorCenterId == centerId).ToList();
    }

    /// <summary>
    /// 註冊新設備
    /// </summary>
    public DeviceStatus RegisterDevice(string centerId, string deviceName, string ipAddress)
    {
        NormalizeCenterDevices();

        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null)
            throw new InvalidOperationException("center-not-found");

        if ((center.Devices?.Count ?? 0) >= 3)
            throw new InvalidOperationException("max-devices-reached");

        var device = new DeviceStatus
        {
            DeviceId = Guid.NewGuid().ToString(),
            VisitorCenterId = centerId,
            DeviceName = deviceName,
            IpAddress = ipAddress,
            IsOnline = true,
            LastSeen = DateTime.Now
        };
        _deviceStatuses.Add(device);

        // 同時將設備加入中心的新 Devices 清單
        center.DeviceIds.Add(device.DeviceId);

        var deviceNumber = (center.Devices?.Count ?? 0) + 1;
        var deviceConfig = new DevicePlayConfig
        {
            DeviceNumber = deviceNumber,
            DeviceId = device.DeviceId,
            DeviceSlug = BuildDefaultSlug(center.Name, deviceNumber),
            Name = deviceName,
            ScreenOrientation = ScreenOrientation.Landscape16x9,
            IsActive = true,
            LastModified = DateTime.Now
        };

        if (center.Devices == null)
            center.Devices = new();

        center.Devices.Add(deviceConfig);
        PersistVisitorCenters();

        PersistDeviceStatuses();

        return device;
    }

    /// <summary>
    /// 為特定設備分配播放版型
    /// </summary>
    public bool AssignPageToDevice(string centerId, int deviceNumber, string pageId)
    {
        NormalizeCenterDevices();

        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null)
            return false;

        var device = center.Devices?.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
        if (device == null)
            return false;

        device.AssignedPageId = pageId;
        device.LastModified = DateTime.Now;
        PersistVisitorCenters();
        return true;
    }


    /// <summary>
    /// 更新設備心跳（設備上線狀態）
    /// </summary>
    public bool UpdateDeviceHeartbeat(string deviceId)
    {
        var device = _deviceStatuses.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device == null) return false;

        device.IsOnline = true;
        device.LastSeen = DateTime.Now;
    PersistDeviceStatuses();
        return true;
    }

    // ============= 推送命令管理 =============

    /// <summary>
    /// 建立推送命令（推送版型配置至設備）
    /// </summary>
    public SignagePushCommand CreatePushCommand(string deviceId, SignagePage pageConfig)
    {
        var command = new SignagePushCommand
        {
            Id = Guid.NewGuid().ToString(),
            DeviceId = deviceId,
            PageConfig = pageConfig,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };
        _pushCommands.Add(command);
        PersistPushCommands();
        return command;
    }

    /// <summary>
    /// 取得待推送的命令
    /// </summary>
    public List<SignagePushCommand> GetPendingCommands(string deviceId)
    {
        return _pushCommands
            .Where(c => c.DeviceId == deviceId && c.Status == "Pending")
            .ToList();
    }

    /// <summary>
    /// 更新推送命令狀態
    /// </summary>
    public bool UpdateCommandStatus(string commandId, string status)
    {
        var command = _pushCommands.FirstOrDefault(c => c.Id == commandId);
        if (command == null) return false;

        command.Status = status;
        if (status == "Sent")
            command.PushedAt = DateTime.Now;
        else if (status == "Acknowledged")
            command.AcknowledgedAt = DateTime.Now;

        PersistPushCommands();
        return true;
    }

    // ============= 內存測試數據初始化 =============

    private void InitializeSampleData()
    {
        // 建立示範遊客中心
        var center1 = CreateVisitorCenter("左營遊客中心", "高雄市左營區");
        var center2 = CreateVisitorCenter("旗津遊客中心", "高雄市旗津區");
        var center3 = CreateVisitorCenter("新北旅遊處", "新北市板橋區");

        // 為每個中心建立預設版型
        var page1 = CreateSignagePage(center1.Id, "三段式版型", LayoutType.TripleVertical);
        page1.ZoneCount = 3;
        page1.Regions = CreateRegionsFromPreset(30, 40, 30);
        page1.Regions[0].ContentType = RegionContentType.MarqueeText;
        page1.Regions[0].TextContent = "★ 歡迎光臨左營遊客中心 ★";
        page1.Regions[1].ContentType = RegionContentType.Video;
        page1.Regions[1].MediaUrl = "https://www.w3schools.com/html/mov_bbb.mp4";
        page1.Regions[2].ContentType = RegionContentType.Image;
        page1.Regions[2].MediaUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=800&q=80";
        page1.TopContent = page1.Regions[0].TextContent;
        page1.MiddleContentUrl = page1.Regions[1].MediaUrl;
        page1.BottomContentUrl = page1.Regions[2].MediaUrl;

        var page2 = CreateSignagePage(center1.Id, "L型資訊版型", LayoutType.LType);
        page2.ZoneCount = 2;
        page2.Regions = CreateRegionsFromPreset(35, 65);
        page2.Regions[0].ContentType = RegionContentType.MarqueeText;
        page2.Regions[0].TextContent = "旗津景點介紹";
        page2.Regions[1].ContentType = RegionContentType.Video;
        page2.Regions[1].MediaUrl = "https://www.w3schools.com/html/mov_bbb.mp4";
        page2.LeftContent = page2.Regions[0].TextContent;
        page2.MiddleContentUrl = page2.Regions[1].MediaUrl;

        ActivatePage(center1.Id, page1.Id);

        // 註冊示範設備並為每個設備分配版型
        // 左營中心：2台看板
        RegisterDevice(center1.Id, "看板-01", "192.168.1.10");
        RegisterDevice(center1.Id, "看板-02", "192.168.1.11");
        
        // 為左營中心的設備分配版型
        AssignPageToDevice(center1.Id, 1, page1.Id);  // 設備1 → page1
        AssignPageToDevice(center1.Id, 2, page2.Id);  // 設備2 → page2

        // 旗津中心：1台看板
        RegisterDevice(center2.Id, "看板-03", "192.168.1.12");

        // 新增媒體素材
        AddMedia(center1.Id, "宣傳影片1", "https://www.w3schools.com/html/mov_bbb.mp4", "Video", 30);
        AddMedia(center1.Id, "景點圖片1", "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800", "Image", 10);
    }

    private void LoadFromStore()
    {
        _visitorCenters = _store.LoadAll<VisitorCenter>(VisitorCentersCategory);
        _signagePages = _store.LoadAll<SignagePage>(SignagePagesCategory);
        _mediaLibrary = _store.LoadAll<MediaContent>(MediaLibraryCategory);
        _deviceStatuses = _store.LoadAll<DeviceStatus>(DeviceStatusesCategory);
        _pushCommands = _store.LoadAll<SignagePushCommand>(PushCommandsCategory);

        NormalizeCenterDevices();
    }

    private void PersistAll()
    {
        PersistVisitorCenters();
        PersistSignagePages();
        PersistMediaLibrary();
        PersistDeviceStatuses();
        PersistPushCommands();
    }

    private void PersistVisitorCenters()
    {
        _store.ReplaceAll(VisitorCentersCategory, _visitorCenters, item => item.Id);
    }

    private void PersistSignagePages()
    {
        _store.ReplaceAll(SignagePagesCategory, _signagePages, item => item.Id);
    }

    private void PersistMediaLibrary()
    {
        _store.ReplaceAll(MediaLibraryCategory, _mediaLibrary, item => item.Id);
    }

    private void PersistDeviceStatuses()
    {
        _store.ReplaceAll(DeviceStatusesCategory, _deviceStatuses, item => item.DeviceId);
    }

    private void PersistPushCommands()
    {
        _store.ReplaceAll(PushCommandsCategory, _pushCommands, item => item.Id);
    }

    private void NormalizeCenterDevices()
    {
        foreach (var center in _visitorCenters)
        {
            center.Devices ??= new();

            if (center.Devices.Count == 0 && center.DeviceIds.Count > 0)
            {
                for (var i = 0; i < center.DeviceIds.Count && i < 3; i++)
                {
                    var deviceId = center.DeviceIds[i];
                    var status = _deviceStatuses.FirstOrDefault(s => s.DeviceId == deviceId);
                    center.Devices.Add(new DevicePlayConfig
                    {
                        DeviceNumber = i + 1,
                        DeviceId = deviceId,
                        DeviceSlug = BuildDefaultSlug(center.Name, i + 1),
                        Name = status?.DeviceName ?? $"設備 {i + 1}",
                        AssignedPageId = center.ActivePageId,
                        IsActive = true,
                        LastModified = DateTime.Now
                    });
                }
            }

            center.Devices = center.Devices
                .OrderBy(d => d.DeviceNumber)
                .Take(3)
                .ToList();

            for (var i = 0; i < center.Devices.Count; i++)
            {
                var d = center.Devices[i];
                if (d.DeviceNumber <= 0)
                    d.DeviceNumber = i + 1;

                if (string.IsNullOrWhiteSpace(d.DeviceSlug))
                    d.DeviceSlug = BuildDefaultSlug(center.Name, d.DeviceNumber);

                if (string.IsNullOrWhiteSpace(d.Name))
                    d.Name = $"設備 {d.DeviceNumber}";

                d.DeviceSlug = NormalizeSlug(d.DeviceSlug);
            }
        }
    }

    private static string BuildDefaultSlug(string centerName, int deviceNumber)
    {
        var key = NormalizeSlug(centerName);
        if (string.IsNullOrWhiteSpace(key))
            key = "center";

        return $"{key}-{deviceNumber:00}";
    }

    private static string NormalizeSlug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "device";

        var sb = new StringBuilder();
        var lastDash = false;

        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        var normalized = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "device" : normalized;
    }

    private static SignagePage CloneAndNormalizePage(SignagePage page)
    {
        var clone = new SignagePage
        {
            Id = page.Id,
            Name = page.Name,
            VisitorCenterId = page.VisitorCenterId,
            Layout = page.Layout,
            ZoneCount = Math.Clamp(page.ZoneCount, 1, 3),
            ScreenOrientation = page.ScreenOrientation,
            MarqueePosition = page.MarqueePosition,
            MarqueeVerticalPercent = page.MarqueeVerticalPercent,
            TopContent = page.TopContent,
            MiddleContentUrl = page.MiddleContentUrl,
            BottomContentUrl = page.BottomContentUrl,
            LeftContent = page.LeftContent,
            RightContent = page.RightContent,
            LastModified = page.LastModified,
            IsActive = page.IsActive,
            Regions = NormalizeRegions(page).Select(CloneRegion).ToList()
        };

        clone.ZoneCount = clone.Regions.Count;
        return clone;
    }

    private static List<SignageRegion> NormalizeRegions(SignagePage page)
    {
        if (page.Regions != null && page.Regions.Count > 0)
        {
            return page.Regions
                .OrderBy(region => region.SortOrder)
                .Take(3)
                .Select(CloneRegion)
                .ToList();
        }

        return page.Layout switch
        {
            LayoutType.SingleFull => CreateLegacySingleRegions(page),
            LayoutType.LType => CreateLegacyDoubleRegions(page),
            _ => CreateLegacyTripleRegions(page)
        };
    }

    private static List<SignageRegion> CreateLegacySingleRegions(SignagePage page)
    {
        var region = new SignageRegion
        {
            Name = "主區塊",
            SortOrder = 0,
            HeightPercent = 100
        };

        if (!string.IsNullOrWhiteSpace(page.MiddleContentUrl))
        {
            region.ContentType = RegionContentType.Video;
            region.MediaUrl = page.MiddleContentUrl;
        }
        else if (!string.IsNullOrWhiteSpace(page.BottomContentUrl))
        {
            region.ContentType = RegionContentType.Image;
            region.MediaUrl = page.BottomContentUrl;
        }
        else if (!string.IsNullOrWhiteSpace(page.TopContent))
        {
            region.ContentType = RegionContentType.MarqueeText;
            region.TextContent = page.TopContent;
        }

        return new List<SignageRegion> { region };
    }

    private static List<SignageRegion> CreateLegacyDoubleRegions(SignagePage page)
    {
        var regions = CreateRegionsFromPreset(35, 65);
        regions[0].ContentType = RegionContentType.MarqueeText;
        regions[0].TextContent = !string.IsNullOrWhiteSpace(page.LeftContent) ? page.LeftContent : page.TopContent;

        if (!string.IsNullOrWhiteSpace(page.MiddleContentUrl))
        {
            regions[1].ContentType = RegionContentType.Video;
            regions[1].MediaUrl = page.MiddleContentUrl;
        }
        else if (!string.IsNullOrWhiteSpace(page.BottomContentUrl))
        {
            regions[1].ContentType = RegionContentType.Image;
            regions[1].MediaUrl = page.BottomContentUrl;
        }

        return regions;
    }

    private static List<SignageRegion> CreateLegacyTripleRegions(SignagePage page)
    {
        var regions = CreateRegionsFromPreset(30, 40, 30);
        regions[0].ContentType = RegionContentType.MarqueeText;
        regions[0].TextContent = page.TopContent;

        if (!string.IsNullOrWhiteSpace(page.MiddleContentUrl))
        {
            regions[1].ContentType = RegionContentType.Video;
            regions[1].MediaUrl = page.MiddleContentUrl;
        }

        if (!string.IsNullOrWhiteSpace(page.BottomContentUrl))
        {
            regions[2].ContentType = RegionContentType.Image;
            regions[2].MediaUrl = page.BottomContentUrl;
        }

        return regions;
    }

    private static List<SignageRegion> CreateRegionsFromPreset(params double[] ratios)
    {
        var names = new[] { "上區塊", "中區塊", "下區塊" };

        return ratios
            .Select((ratio, index) => new SignageRegion
            {
                Name = names[index],
                SortOrder = index,
                HeightPercent = ratio,
                ContentType = RegionContentType.Empty
            })
            .ToList();
    }

    private static SignageRegion CloneRegion(SignageRegion region)
    {
        return new SignageRegion
        {
            Id = region.Id,
            Name = region.Name,
            SortOrder = region.SortOrder,
            HeightPercent = region.HeightPercent,
            ContentType = region.ContentType,
            MediaId = region.MediaId,
            MediaUrl = region.MediaUrl,
            TextContent = region.TextContent
        };
    }
}
