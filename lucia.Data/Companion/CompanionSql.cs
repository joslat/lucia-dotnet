using System.Data.Common;

namespace lucia.Data.Companion;

internal static class CompanionSql
{
    public static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql, params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = connection is Npgsql.NpgsqlConnection ? CompanionSchema.QualifyPostgres(sql) : sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
        }
        return command;
    }
}
