using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EntityCatalog.Services;

internal static class PostgresForeignKeyViolation
{
    public static bool IsViolation(DbUpdateException exception, string constraintName)
    {
        return exception.GetBaseException() is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation &&
               postgresException.ConstraintName == constraintName;
    }
}
