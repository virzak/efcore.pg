namespace Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;

public class NpgsqlAnnotationProvider : RelationalAnnotationProvider
{
    public NpgsqlAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        if (!designTime)
        {
            yield break;
        }

        // Model validation ensures that these facets are the same on all mapped entity types
        var entityType = table.EntityTypeMappings.First().EntityType;

        if (entityType.GetIsUnlogged())
        {
            yield return new Annotation(NpgsqlAnnotationNames.UnloggedTable, entityType.GetIsUnlogged());
        }

        if (entityType[CockroachDbAnnotationNames.InterleaveInParent] is not null)
        {
            yield return new Annotation(CockroachDbAnnotationNames.InterleaveInParent, entityType[CockroachDbAnnotationNames.InterleaveInParent]);
        }

        foreach (var storageParamAnnotation in entityType.GetAnnotations()
                     .Where(a => a.Name.StartsWith(NpgsqlAnnotationNames.StorageParameterPrefix, StringComparison.Ordinal)))
        {
            yield return storageParamAnnotation;
        }
    }

    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        if (!designTime)
        {
            yield break;
        }

        var table = StoreObjectIdentifier.Table(column.Table.Name, column.Table.Schema);
        var valueGeneratedProperty = column.PropertyMappings.Where(
                m =>
                    m.TableMapping.IsSharedTablePrincipal && m.TableMapping.EntityType == m.Property.DeclaringEntityType)
            .Select(m => m.Property)
            .FirstOrDefault(
                p => p.GetValueGenerationStrategy(table) switch
                {
                    NpgsqlValueGenerationStrategy.IdentityByDefaultColumn => true,
                    NpgsqlValueGenerationStrategy.IdentityAlwaysColumn    => true,
                    NpgsqlValueGenerationStrategy.SerialColumn            => true,
                    _                                                     => false
                });

        if (valueGeneratedProperty is not null)
        {
            var valueGenerationStrategy = valueGeneratedProperty.GetValueGenerationStrategy();
            yield return new Annotation(NpgsqlAnnotationNames.ValueGenerationStrategy, valueGenerationStrategy);

            if (valueGenerationStrategy == NpgsqlValueGenerationStrategy.IdentityByDefaultColumn ||
                valueGenerationStrategy == NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
            {
                if (valueGeneratedProperty[NpgsqlAnnotationNames.IdentityOptions] is string identityOptions)
                {
                    yield return new Annotation(NpgsqlAnnotationNames.IdentityOptions, identityOptions);
                }
            }
        }

        // If the property has a collation explicitly defined on it via the standard EF mechanism, it will get
        // passed on the Collation property (we don't need to do anything).
        // Otherwise, a model-wide default column collation exists, pass that through our custom annotation.
        if (column.PropertyMappings.All(m => m.Property.GetCollation() is null) &&
            column.PropertyMappings.Select(m => m.Property.GetDefaultCollation())
                .FirstOrDefault(c => c is not null) is { } defaultColumnCollation)
        {
            yield return new Annotation(NpgsqlAnnotationNames.DefaultColumnCollation, defaultColumnCollation);
        }

        if (column.PropertyMappings.Select(m => m.Property.GetTsVectorConfig())
                .FirstOrDefault(c => c is not null) is { } tsVectorConfig)
        {
            yield return new Annotation(NpgsqlAnnotationNames.TsVectorConfig, tsVectorConfig);
        }

        valueGeneratedProperty = column.PropertyMappings.Select(m => m.Property)
            .FirstOrDefault(p => p.GetTsVectorProperties() is not null);
        if (valueGeneratedProperty is not null)
        {
            var tableIdentifier = StoreObjectIdentifier.Table(column.Table.Name, column.Table.Schema);

            yield return new Annotation(
                NpgsqlAnnotationNames.TsVectorProperties,
                valueGeneratedProperty.GetTsVectorProperties()!
                    .Select(p2 => valueGeneratedProperty.DeclaringEntityType.FindProperty(p2)!.GetColumnName(tableIdentifier))
                    .ToArray());
        }

        // Model validation ensures that these facets are the same on all mapped properties
        var property = column.PropertyMappings.First().Property;

        if (property.GetCompressionMethod() is { } compressionMethod)
        {
            yield return new Annotation(NpgsqlAnnotationNames.CompressionMethod, compressionMethod);
        }
    }

    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        if (!designTime)
        {
            yield break;
        }

        // Model validation ensures that these facets are the same on all mapped indexes
        var modelIndex = index.MappedIndexes.First();

        if (modelIndex.GetCollation() is { } collation)
        {
            yield return new Annotation(RelationalAnnotationNames.Collation, collation);
        }

        if (modelIndex.GetMethod() is { } method)
        {
            yield return new Annotation(NpgsqlAnnotationNames.IndexMethod, method);
        }

        if (modelIndex.GetOperators() is { } operators)
        {
            yield return new Annotation(NpgsqlAnnotationNames.IndexOperators, operators);
        }

        if (modelIndex.GetSortOrder() is { } sortOrder)
        {
            yield return new Annotation(NpgsqlAnnotationNames.IndexSortOrder, sortOrder);
        }

        if (modelIndex.GetNullSortOrder() is { } nullSortOrder)
        {
            yield return new Annotation(NpgsqlAnnotationNames.IndexNullSortOrder, nullSortOrder);
        }

        if (modelIndex.GetTsVectorConfig() is { } configName)
        {
            yield return new Annotation(NpgsqlAnnotationNames.TsVectorConfig, configName);
        }

        if (modelIndex.GetIncludeProperties() is { } includeProperties)
        {
            var tableIdentifier = StoreObjectIdentifier.Table(index.Table.Name, index.Table.Schema);

            yield return new Annotation(
                NpgsqlAnnotationNames.IndexInclude,
                includeProperties
                    .Select(p => modelIndex.DeclaringEntityType.FindProperty(p)!.GetColumnName(tableIdentifier))
                    .ToArray());
        }

        var isCreatedConcurrently = modelIndex.IsCreatedConcurrently();
        if (isCreatedConcurrently.HasValue)
        {
            yield return new Annotation(
                NpgsqlAnnotationNames.CreatedConcurrently,
                isCreatedConcurrently.Value);
        }
    }

    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        if (!designTime)
        {
            return Array.Empty<IAnnotation>();
        }

        return model.Model.GetAnnotations().Where(
            a =>
                a.Name.StartsWith(NpgsqlAnnotationNames.PostgresExtensionPrefix, StringComparison.Ordinal)
                || a.Name.StartsWith(NpgsqlAnnotationNames.EnumPrefix, StringComparison.Ordinal)
                || a.Name.StartsWith(NpgsqlAnnotationNames.RangePrefix, StringComparison.Ordinal)
                || a.Name.StartsWith(NpgsqlAnnotationNames.CollationDefinitionPrefix, StringComparison.Ordinal));
    }
}