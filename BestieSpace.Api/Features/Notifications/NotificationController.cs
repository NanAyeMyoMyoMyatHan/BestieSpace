using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IFcmService _fcmService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        IFcmService fcmService,
        ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _fcmService = fcmService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationListResponseDto>> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var result = await _notificationService.GetUserNotificationsAsync(currentUserId.Value, pageNumber, pageSize, unreadOnly);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var count = await _notificationService.GetUnreadNotificationCountAsync(currentUserId.Value);
            return Ok(new { unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread notification count for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/read")]
    public async Task<ActionResult> MarkAsRead(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var success = await _notificationService.MarkNotificationAsReadAsync(id, currentUserId.Value);
            if (!success)
            {
                return NotFound(new { message = "Notification not found or access denied." });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<int>> MarkAllAsRead()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var markedCount = await _notificationService.MarkAllNotificationsAsReadAsync(currentUserId.Value);
            return Ok(new { markedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("fcm-config")]
    [AllowAnonymous]
    public ActionResult<FcmWebConfigDto> GetFcmWebConfig()
    {
        try
        {
            var config = _fcmService.GetWebConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving FCM web config");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("fcm-token")]
    public async Task<ActionResult> RegisterFcmToken([FromBody] RegisterFcmTokenRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { message = "FCM device token is required." });
        }

        try
        {
            var success = await _fcmService.RegisterTokenAsync(currentUserId.Value, request.Token, request.DeviceType ?? "Web", request.DeviceModel);
            return Ok(new { success, message = "FCM device token registered successfully! 🌸" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering FCM token for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("fcm-token")]
    public async Task<ActionResult> UnregisterFcmToken([FromBody] UnregisterFcmTokenRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Token))
        {
            return BadRequest(new { message = "FCM device token is required." });
        }

        try
        {
            var success = await _fcmService.UnregisterTokenAsync(currentUserId.Value, request.Token);
            return Ok(new { success, message = "FCM device token unregistered." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering FCM token for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-push")]
    public async Task<ActionResult<FcmPushResultDto>> SendTestPush([FromBody] SendPushTestRequestDto? request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var title = !string.IsNullOrWhiteSpace(request?.Title) ? request.Title : "BestieSpace Realtime Alert 🌸";
            var message = !string.IsNullOrWhiteSpace(request?.Message) ? request.Message : "FCM Realtime push notification is working like magic! 💕✨";

            var devicesDelivered = await _fcmService.SendPushToUserAsync(currentUserId.Value, title, message, request?.ClickUrl ?? "/");

            return Ok(new FcmPushResultDto
            {
                Success = true,
                DevicesTargeted = devicesDelivered,
                Message = devicesDelivered > 0 
                    ? $"Push sent successfully to {devicesDelivered} device(s)! 🌸"
                    : "Push processed! (Register a browser/device token to receive native OS push notifications)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test push to user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("userId")?.Value;

        if (int.TryParse(claim, out var uid))
        {
            return uid;
        }
        return null;
    }
}
