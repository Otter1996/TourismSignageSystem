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

/// <summary>
/// 媒體內容（素材）
/// </summary>
public class MediaContent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>媒體類型：Image、Video、Text</summary>
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
    
    /// <summary>該中心的看板裝置 ID 列表</summary>
    public List<string> DeviceIds { get; set; } = new();
    
    /// <summary>當前啟用的版型配置 ID</summary>
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