using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CuMusicClub.Infrastructure.Data.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
}
