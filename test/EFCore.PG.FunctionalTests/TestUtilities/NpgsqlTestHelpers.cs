﻿using Npgsql.EntityFrameworkCore.PostgreSQL.Diagnostics.Internal;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.TestUtilities;

public class NpgsqlTestHelpers : TestHelpers
{
    protected NpgsqlTestHelpers() {}

    public static NpgsqlTestHelpers Instance { get; } = new();

    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkNpgsql();

    public override void UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql(new NpgsqlConnection("Host=localhost;Database=DummyDatabase"));

    public override LoggingDefinitions LoggingDefinitions { get; } = new NpgsqlLoggingDefinitions();
}