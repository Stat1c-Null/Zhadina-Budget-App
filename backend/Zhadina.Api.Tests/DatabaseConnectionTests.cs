using Npgsql;
using Zhadina.Api.Infrastructure;

namespace Zhadina.Api.Tests;

public class DatabaseConnectionTests
{
    [Theory]
    [InlineData("postgres://")]
    [InlineData("postgresql://")]
    public void ToNpgsql_ConvertsReplitStyleUri(string scheme)
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.ToNpgsql($"{scheme}app%40user:p%40ss:w0rd@db.example.com:6543/budget?sslmode=require"));

        Assert.Equal("db.example.com", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("budget", result.Database);
        Assert.Equal("app@user", result.Username);
        Assert.Equal("p@ss:w0rd", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void ToNpgsql_DefaultsPortTo5432()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.ToNpgsql("postgres://u:p@db.example.com/budget"));

        Assert.Equal(5432, result.Port);
    }

    [Fact]
    public void ToNpgsql_PassesKeyValueStringsThrough()
    {
        const string keyValue = "Host=localhost;Database=zhadina_dev;Username=postgres";

        Assert.Equal(keyValue, DatabaseConnection.ToNpgsql(keyValue));
    }
}
