using Signage.Common;
using System.Collections.Generic;
using System.Linq;

namespace Signage.Server.Services;

/// <summary>
/// 電子看板數據服務（簡化版：使用內存存儲，實際應改用資料庫）
/// </summary>
public class SignageDataService
{
    // 模擬數據存儲
    private List<VisitorCenter> _visitorCenters = new();
    private List<SignagePage> _signagePages = new();
    private List<MediaContent> _mediaLibrary = new();
    private List<DeviceStatus> _deviceStatuses = new();
    private List<SignagePushCommand> _pushCommands = new();

    public SignageDataService()
    {
        InitializeSampleData();
    }

    // ============= 遊客中心管理 =============

    /// <summary>
    /// 取得所有遊客中心
    /// </summary>
    public List<VisitorCenter> GetAllVisitorCenters()
    {
        return _visitorCenters.ToList();
    }

    /// <summary>
    /// 取得單一遊客中心
    /// </summary>
    public VisitorCenter? GetVisitorCenter(string centerId)
    {
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
        return true;
    }

    // ============= 版型配置管理 =============

    /// <summary>
    /// 取得指定中心的所有版型配置
    /// </summary>
    public List<SignagePage> GetPagesByCenter(string centerId)
    {
        return _signagePages.Where(p => p.VisitorCenterId == centerId).ToList();
    }

    /// <summary>
    /// 取得單一版型配置
    /// </summary>
    public SignagePage? GetSignagePage(string pageId)
    {
        return _signagePages.FirstOrDefault(p => p.Id == pageId);
    }

    /// <summary>
    /// 建立新版型配置
    /// </summary>
    public SignagePage CreateSignagePage(string centerId, string name, LayoutType layout)
    {
        var page = new SignagePage
        {
            Id = Guid.NewGuid().ToString(),
            VisitorCenterId = centerId,
            Name = name,
            Layout = layout,
            IsActive = true,
            LastModified = DateTime.Now
        };
        _signagePages.Add(page);
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
        page.TopContent = updatedPage.TopContent;
        page.MiddleContentUrl = updatedPage.MiddleContentUrl;
        page.BottomContentUrl = updatedPage.BottomContentUrl;
        page.LeftContent = updatedPage.LeftContent;
        page.RightContent = updatedPage.RightContent;
        page.LastModified = DateTime.Now;
        return true;
    }

    /// <summary>
    /// 啟用版型配置
    /// </summary>
    public bool ActivatePage(string centerId, string pageId)
    {
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center == null) return false;

        center.ActivePageId = pageId;
        center.LastModified = DateTime.Now;
        return true;
    }

    // ============= 媒體庫管理 =============

    /// <summary>
    /// 取得指定中心的媒體庫
    /// </summary>
    public List<MediaContent> GetMediaByCenter(string centerId)
    {
        return _mediaLibrary.Where(m => m.Id.StartsWith(centerId)).ToList();
    }

    /// <summary>
    /// 上傳媒體素材
    /// </summary>
    public MediaContent AddMedia(string centerId, string title, string url, string mediaType, int duration = 10)
    {
        var media = new MediaContent
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Url = url,
            MediaType = mediaType,
            Duration = duration,
            UploadedAt = DateTime.Now
        };
        _mediaLibrary.Add(media);
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

        // 同時將設備 ID 加入中心的設備列表
        var center = _visitorCenters.FirstOrDefault(c => c.Id == centerId);
        if (center != null)
        {
            center.DeviceIds.Add(device.DeviceId);
        }

        return device;
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
        page1.TopContent = "★ 歡迎光臨左營遊客中心 ★";
        page1.MiddleContentUrl = "https://www.w3schools.com/html/mov_bbb.mp4";
        page1.BottomContentUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=800&q=80";

        var page2 = CreateSignagePage(center1.Id, "L型資訊版型", LayoutType.LType);
        page2.MiddleContentUrl = "https://www.w3schools.com/html/mov_bbb.mp4";
        page2.LeftContent = "旗津景點介紹";

        ActivatePage(center1.Id, page1.Id);

        // 註冊示範設備
        RegisterDevice(center1.Id, "看板-01", "192.168.1.10");
        RegisterDevice(center1.Id, "看板-02", "192.168.1.11");
        RegisterDevice(center2.Id, "看板-03", "192.168.1.12");

        // 新增媒體素材
        AddMedia(center1.Id, "宣傳影片1", "https://www.w3schools.com/html/mov_bbb.mp4", "Video", 30);
        AddMedia(center1.Id, "景點圖片1", "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800", "Image", 10);
    }
}
