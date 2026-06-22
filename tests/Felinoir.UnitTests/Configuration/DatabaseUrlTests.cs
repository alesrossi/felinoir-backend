using Felinoir.Infrastructure.Configuration;
using Npgsql;

namespace Felinoir.UnitTests.Configuration;

public class DatabaseUrlTests
{
    [Fact]
    public void Converts_uri_form_to_npgsql_keyword_string()
    {
        var result = DatabaseUrl.ToNpgsqlConnectionString(
            "postgres://alice:s3cret@db.example.com:5433/felinoir?sslmode=require");

        var b = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.example.com", b.Host);
        Assert.Equal(5433, b.Port);
        Assert.Equal("alice", b.Username);
        Assert.Equal("s3cret", b.Password);
        Assert.Equal("felinoir", b.Database);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Defaults_port_to_5432_when_omitted()
    {
        var result = DatabaseUrl.ToNpgsqlConnectionString(
            "postgresql://bob:pw@localhost/felinoir");

        var b = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(5432, b.Port);
        Assert.Equal("localhost", b.Host);
    }

    [Fact]
    public void Url_decodes_password_special_characters()
    {
        var result = DatabaseUrl.ToNpgsqlConnectionString(
            "postgres://user:p%40ss%3Aword@localhost:5432/felinoir");

        var b = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("p@ss:word", b.Password);
    }

    [Fact]
    public void Maps_verify_full_sslmode()
    {
        var result = DatabaseUrl.ToNpgsqlConnectionString(
            "postgres://u:p@h:5432/db?sslmode=verify-full");

        var b = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(SslMode.VerifyFull, b.SslMode);
    }

    [Fact]
    public void Passes_keyword_form_through_unchanged()
    {
        const string keyword = "Host=localhost;Port=5432;Username=u;Password=p;Database=felinoir";

        var result = DatabaseUrl.ToNpgsqlConnectionString(keyword);

        Assert.Equal(keyword, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Throws_when_empty(string? value)
    {
        Assert.Throws<ArgumentException>(() => DatabaseUrl.ToNpgsqlConnectionString(value));
    }
}
