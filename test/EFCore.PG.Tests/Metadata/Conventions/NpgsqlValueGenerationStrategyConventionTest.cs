﻿using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.FunctionalTests;
using Xunit;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Tests.Metadata.Conventions
{
    public class NpgsqlValueGenerationStrategyConventionTest
    {
        [Fact]
        public void Annotations_are_added_when_conventional_model_builder_is_used()
        {
            var model = NpgsqlTestHelpers.Instance.CreateConventionBuilder().Model;

            Assert.Equal(1, model.GetAnnotations().Count());

            Assert.Equal(NpgsqlAnnotationNames.ValueGenerationStrategy, model.GetAnnotations().Single().Name);
            Assert.Equal(NpgsqlValueGenerationStrategy.SerialColumn, model.GetAnnotations().Single().Value);
        }

        [Fact]
        public void Annotations_are_added_when_conventional_model_builder_is_used_with_sequences()
        {
            var model = NpgsqlTestHelpers.Instance.CreateConventionBuilder()
                .ForNpgsqlUseSequenceHiLo()
                .Model;

            var annotations = model.GetAnnotations().OrderBy(a => a.Name);
            Assert.Equal(3, annotations.Count());

            // Note that the annotation order is different with Npgsql than the SqlServer (N vs. S...)
            Assert.Equal(NpgsqlAnnotationNames.HiLoSequenceName, annotations.ElementAt(0).Name);
            Assert.Equal(NpgsqlModelAnnotations.DefaultHiLoSequenceName, annotations.ElementAt(0).Value);

            Assert.Equal(NpgsqlAnnotationNames.ValueGenerationStrategy, annotations.ElementAt(1).Name);
            Assert.Equal(NpgsqlValueGenerationStrategy.SequenceHiLo, annotations.ElementAt(1).Value);

            Assert.Equal(
                RelationalAnnotationNames.SequencePrefix +
                "." +
                NpgsqlModelAnnotations.DefaultHiLoSequenceName,
                annotations.ElementAt(2).Name);
            Assert.NotNull(annotations.ElementAt(2).Value);
        }
    }
}
