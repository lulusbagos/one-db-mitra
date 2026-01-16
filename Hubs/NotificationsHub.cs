using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace one_db_mitra.Hubs
{
    [Authorize]
    public class NotificationsHub : Hub
    {
    }
}
