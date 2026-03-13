using IotGrpcLearning.Infrastructure;
using System;
using System.Collections.Generic;
using Xunit;

namespace IotGrpcLearning.Tests;

public class FilterBuilderTests
{
    [Fact]
    public void BuildFilterQuery_EmptyFilters_ReturnsEmpty()
    {
        // Arrange
        var helper = new Helper();
        
        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", new Dictionary<string, string[]>());
        
        // Assert
        Assert.Equal(string.Empty, query);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildFilterQuery_NullFilters_ReturnsEmpty()
    {
        // Arrange
        var helper = new Helper();
        
        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", null!);
        
        // Assert
        Assert.Equal(string.Empty, query);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildFilterQuery_SingleColumnSingleValue_GeneratesCorrectSQL()
    {
        // Arrange
        var helper = new Helper();
        var filters = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "alpha" }
        };

        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", filters);

        // Assert
        Assert.Equal(" WHERE (\"name\" LIKE @p0)", query);
        Assert.Single(parameters);
        Assert.Equal("@p0", parameters[0].ParameterName);
        Assert.Equal("%alpha%", parameters[0].Value);
    }

    [Fact]
    public void BuildFilterQuery_SingleColumnMultipleValues_GeneratesORConditions()
    {
        // Arrange
        var helper = new Helper();
        var filters = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "alpha", "beta" }
        };

        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", filters);

        // Assert
        Assert.Equal(" WHERE (\"name\" LIKE @p0 OR \"name\" LIKE @p1)", query);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("@p0", parameters[0].ParameterName);
        Assert.Equal("%alpha%", parameters[0].Value);
        Assert.Equal("@p1", parameters[1].ParameterName);
        Assert.Equal("%beta%", parameters[1].Value);
    }

    [Fact]
    public void BuildFilterQuery_MultipleColumns_GeneratesANDConditions()
    {
        // Arrange
        var helper = new Helper();
        var filters = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "alpha" },
            ["status"] = new[] { "active" }
        };

        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", filters);

        // Assert
        Assert.Contains("WHERE", query);
        Assert.Contains("\"name\" LIKE @p0", query);
        Assert.Contains("\"status\" LIKE @p1", query);
        Assert.Contains(" AND ", query);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void BuildFilterQuery_MultipleColumnsMultipleValues_GeneratesComplexConditions()
    {
        // Arrange
        var helper = new Helper();
        var filters = new Dictionary<string, string[]>
        {
            ["name"] = new[] { "alpha", "beta" },
            ["status"] = new[] { "active", "pending", "complete" }
        };

        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", filters);

        // Assert
        // Should generate: WHERE ("name" LIKE @p0 OR "name" LIKE @p1) AND ("status" LIKE @p2 OR "status" LIKE @p3 OR "status" LIKE @p4)
        Assert.Contains("WHERE", query);
        Assert.Contains("\"name\" LIKE @p0 OR \"name\" LIKE @p1", query);
        Assert.Contains("\"status\" LIKE @p2 OR \"status\" LIKE @p3 OR \"status\" LIKE @p4", query);
        Assert.Equal(5, parameters.Count);
    }

    [Fact]
    public void BuildFilterQuery_EmptyValueArray_SkipsColumn()
    {
        // Arrange
        var helper = new Helper();
        var filters = new Dictionary<string, string[]>
        {
            ["name"] = Array.Empty<string>(),
            ["status"] = new[] { "active" }
        };

        // Act
        var (query, parameters) = helper.BuildFilterQuery("Projects", filters);

        // Assert
        Assert.Equal(" WHERE (\"status\" LIKE @p0)", query);
        Assert.Single(parameters);
    }
}