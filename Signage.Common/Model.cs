using System;
using System.Collections.Generic;

namespace Signage.Common;

/// <summary>
/// 版型種類枚舉
/// </summary>
public enum LayoutType
{
    /// <summary>全螢幕混合式：滿版背景影片，上方疊加透明跑馬燈文字</summary>
    SingleFull,
    
    /// <summary>經典三段式：頂部跑馬燈 (30%)、中部影片 (40%)、底部圖片 (30%)</summary>
    TripleVertical,
    
    /// <summary>L型資訊式：主畫面播放影片，側邊或下方顯示特定資訊</summary>
    LType
}

public enum ScreenOrientation
{
    Landscape16x9,
    Portrait9x16
}

public enum MarqueeVerticalPosition
{
    Top,
    Middle,
    Bottom
}

public enum RegionContentType
{
    Empty,
    Video,
    Image,
    MarqueeText,
    Clock
}

public class SignageRegion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "區塊";
    public int SortOrder { get; set; }
    public double HeightPercent { get; set; } = 33;
    public RegionContentType ContentType { get; set; } = RegionContentType.Empty;
    public string MediaId { get; set; } = "";
    public string MediaUrl { get; set; } = "";
    public List<string> MediaIds { get; set; } = new();
    public List<string> MediaUrls { get; set; } = new();
    public string TextContent { get; set; } = "";
}

public class DeviceRegionContent
{
    public int SortOrder { get; set; }
    public RegionContentType ContentType { get; set; } = RegionContentType.Empty;
    public string TextContent { get; set; } = "";
    public List<string> MediaIds { get; set; } = new();
    public List<string> MediaUrls { get; set; } = new();
}

/// <summary>
/// 電子看板設備播放配置 — 每台設備一個配置，同一中心內可有多台
/// </summary>
public class DevicePlayConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>同中心內的設備編號（1, 2, 3...）</summary>
    public int DeviceNumber { get; set; } = 1;
    
    /// <summary>物理設備的唯一識別碼（如 device_center_A_01）</summary>
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>公開播放網址代碼（如 hj-01）</summary>
    public string DeviceSlug { get; set; } = "";
    
    /// <summary>設備名稱（如「大廳看板」、「入口看板」）</summary>
    public string Name { get; set; } = "";
    
    /// <summary>分配給此設備的版型 ID</summary>
    public string AssignedPageId { get; set; } = "";
    
    /// <summary>該設備的預設螢幕方向</summary>
    public ScreenOrientation ScreenOrientation { get; set; } = ScreenOrientation.Landscape16x9;
    
    /// <summary>設備是否啟用</summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>最後修改時間</summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>設備專屬內容（依版型區塊排序）</summary>
    public List<DeviceRegionContent> RegionContents { get; set; } = new();
}

/// <summary>
/// 中心 > 設備 的管理檢視模型
/// </summary>
public class DeviceHierarchyNode
{
    public string CenterId { get; set; } = "";
    public string CenterName { get; set; } = "";
    public int DeviceNumber { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceSlug { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string AssignedPageId { get; set; } = "";
    public string AssignedPageName { get; set; } = "";
    public ScreenOrientation ScreenOrientation { get; set; } = ScreenOrientation.Landscape16x9;
    public bool IsActive { get; set; } = true;
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public List<DeviceRegionContent> RegionContents { get; set; } = new();
}

/// <summary>
/// 媒體內容（素材）
/// </summary>
public class MediaContent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VisitorCenterId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>媒體類型：Image、Video、File、Link、Text</summary>
    public string MediaType { get; set; } = "Image";
    /// <summary>播放持續時間（秒）</summary>
    public int Duration { get; set; } = 10;
    /// <summary>上傳時間</summary>
    public DateTime UploadedAt { get; set; } = DateTime.Now;
    /// <summary>文件大小（字節）</summary>
    public long FileSize { get; set; } = 0;
}

/// <summary>
/// 版型配置（看板頁面配置）
/// </summary>
public class SignagePage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "預設版型";
    public string VisitorCenterId { get; set; } = "";
    public LayoutType Layout { get; set; } = LayoutType.TripleVertical;
    public int ZoneCount { get; set; } = 3;
    public ScreenOrientation ScreenOrientation { get; set; } = ScreenOrientation.Landscape16x9;
    public MarqueeVerticalPosition MarqueePosition { get; set; } = MarqueeVerticalPosition.Top;
    public double MarqueeVerticalPercent { get; set; } = 10;
    public List<SignageRegion> Regions { get; set; } = new();

    /// <summary>頂部區域：跑馬燈文字</summary>
    public string TopContent { get; set; } = "";
    
    /// <summary>中部區域：影片 URL 或媒體ID</summary>
    public string MiddleContentUrl { get; set; } = "";
    
    /// <summary>底部區域：圖片 URL 或媒體ID</summary>
    public string BottomContentUrl { get; set; } = "";

    /// <summary>左側區域（L型版型）：資訊內容</summary>
    public string LeftContent { get; set; } = "";
    
    /// <summary>右側區域（L型版型）：資訊內容</summary>
    public string RightContent { get; set; } = "";

    /// <summary>最後修改時間</summary>
    public DateTime LastModified { get; set; } = DateTime.Now;
    
    /// <summary>是否啟用</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 遊客中心（客戶端點）
/// </summary>
public class VisitorCenter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    
    /// <summary>該中心的電子看板設備配置列表（支持 1~3 台看板）</summary>
    public List<DevicePlayConfig> Devices { get; set; } = new();
    
    /// <summary>該中心的看板裝置 ID 列表（向後相容）</summary>
    public List<string> DeviceIds { get; set; } = new();
    
    /// <summary>當前啟用的版型配置 ID（向後相容）</summary>
    public string ActivePageId { get; set; } = "";
    
    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>最後修改時間</summary>
    public DateTime LastModified { get; set; } = DateTime.Now;
}

/// <summary>
/// 看板設備狀態
/// </summary>
public class DeviceStatus
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string VisitorCenterId { get; set; } = "";
    
    /// <summary>設備是否在線</summary>
    public bool IsOnline { get; set; }
    
    /// <summary>當前播放的內容 ID</summary>
    public string CurrentPlayingId { get; set; } = "";
    
    /// <summary>最後主動連線時間</summary>
    public DateTime LastSeen { get; set; }
    
    /// <summary>設備 IP 地址</summary>
    public string IpAddress { get; set; } = "";
    
    /// <summary>上次推送配置的時間</summary>
    public DateTime LastPushTime { get; set; }
}

/// <summary>
/// 媒體庫（管理素材集合）
/// </summary>
public class MediaLibrary
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VisitorCenterId { get; set; } = "";
    
    /// <summary>媒體集合</summary>
    public List<MediaContent> Videos { get; set; } = new();
    public List<MediaContent> Images { get; set; } = new();
    public List<MediaContent> TextContent { get; set; } = new();
    
    /// <summary>最後更新時間</summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

/// <summary>
/// 看板推送命令（用於遠端更新看板配置）
/// </summary>
public class SignagePushCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = "";
    public SignagePage PageConfig { get; set; } = new();
    
    /// <summary>命令狀態：Pending、Sent、Acknowledged、Failed</summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>建立時間</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>推送時間</summary>
    public DateTime? PushedAt { get; set; }
    
    /// <summary>設備確認時間</summary>
    public DateTime? AcknowledgedAt { get; set; }
}