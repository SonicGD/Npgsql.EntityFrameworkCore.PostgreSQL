﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.FunctionalTests
{
    public class BuiltInDataTypesNpgsqlFixture : BuiltInDataTypesFixtureBase
    {
        private readonly DbContextOptions _options;
        private readonly NpgsqlTestStore _testStore;
        private readonly TestSqlLoggerFactory _testSqlLoggerFactory = new TestSqlLoggerFactory();

        public BuiltInDataTypesNpgsqlFixture()
        {
            _testStore = NpgsqlTestStore.Create("BuiltInDataTypes");

            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkNpgsql()
                .AddSingleton(TestModelSource.GetFactory(OnModelCreating))
                .AddSingleton<ILoggerFactory>(_testSqlLoggerFactory)
                .BuildServiceProvider();

            _options = new DbContextOptionsBuilder()
                .UseNpgsql(_testStore.Connection, b => b.ApplyConfiguration())
                .EnableSensitiveDataLogging()
                .UseInternalServiceProvider(serviceProvider)
                .Options;

            using (var context = new DbContext(_options))
            {
                context.Database.EnsureCreated();
            }
        }

        public override DbContext CreateContext()
        {
            var context = new DbContext(_options);
            context.Database.UseTransaction(_testStore.Transaction);
            return context;
        }

        public override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("hstore");

            MakeRequired<MappedDataTypes>(modelBuilder);

            modelBuilder.Entity<BuiltInDataTypes>(b =>
            {
                b.Ignore(dt => dt.TestUnsignedInt16);
                b.Ignore(dt => dt.TestUnsignedInt32);
                b.Ignore(dt => dt.TestUnsignedInt64);
                b.Ignore(dt => dt.TestCharacter);
                b.Ignore(dt => dt.TestSignedByte);
                b.Ignore(dt => dt.TestDateTimeOffset);
                b.Ignore(dt => dt.TestByte);
            });

            modelBuilder.Entity<BuiltInNullableDataTypes>(b =>
            {
                b.Ignore(dt => dt.TestNullableUnsignedInt16);
                b.Ignore(dt => dt.TestNullableUnsignedInt32);
                b.Ignore(dt => dt.TestNullableUnsignedInt64);
                b.Ignore(dt => dt.TestNullableCharacter);
                b.Ignore(dt => dt.TestNullableSignedByte);
                b.Ignore(dt => dt.TestNullableDateTimeOffset);
                b.Ignore(dt => dt.TestNullableByte);
            });

            modelBuilder.Entity<MappedDataTypes>(b =>
            {
                b.HasKey(e => e.Int);
                b.Property(e => e.Int)
                 .ValueGeneratedNever();
            });

            modelBuilder.Entity<MappedNullableDataTypes>(b =>
            {
                b.HasKey(e => e.Int);
                b.Property(e => e.Int)
                 .ValueGeneratedNever();
            });

            modelBuilder.Entity<MappedSizedDataTypes>()
                .Property(e => e.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<MappedScaledDataTypes>()
                .Property(e => e.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<MappedPrecisionAndScaledDataTypes>()
                .Property(e => e.Id)
                .ValueGeneratedNever();

            MapColumnTypes<MappedDataTypes>(modelBuilder);
            MapColumnTypes<MappedNullableDataTypes>(modelBuilder);

            MapSizedColumnTypes<MappedSizedDataTypes>(modelBuilder);
            MapSizedColumnTypes<MappedScaledDataTypes>(modelBuilder);
            MapPreciseColumnTypes<MappedPrecisionAndScaledDataTypes>(modelBuilder);

            // MapColumnTypes automatically mapped column types based on the property name, but
            // this doesn't work for Tinyint. Remap.
            modelBuilder.Entity<MappedDataTypes>().Property(e => e.Tinyint).HasColumnType("smallint");
            modelBuilder.Entity<MappedNullableDataTypes>().Property(e => e.Tinyint).HasColumnType("smallint");

            // Jsonb in .NET is a regular string
            modelBuilder.Entity<MappedDataTypes>().Property(e => e.Jsonb).HasColumnType("jsonb");
            modelBuilder.Entity<MappedNullableDataTypes>().Property(e => e.Jsonb).HasColumnType("jsonb");

            // Arrays
            modelBuilder.Entity<MappedDataTypes>().Property(e => e.PrimitiveArray).HasColumnType("_int4");
            modelBuilder.Entity<MappedNullableDataTypes>().Property(e => e.PrimitiveArray).HasColumnType("_int4");
            modelBuilder.Entity<MappedDataTypes>().Property(e => e.NonPrimitiveArray).HasColumnType("_macaddr");
            modelBuilder.Entity<MappedNullableDataTypes>().Property(e => e.NonPrimitiveArray).HasColumnType("_macaddr");

            modelBuilder.Entity<MappedDataTypes>().Property(e => e.Xid).HasColumnType("xid");
            modelBuilder.Entity<MappedNullableDataTypes>().Property(e => e.Xid).HasColumnType("xid");
        }

        private static void MapColumnTypes<TEntity>(ModelBuilder modelBuilder) where TEntity : class
        {
            var entityType = modelBuilder.Entity<TEntity>().Metadata;

            foreach (var propertyInfo in entityType.ClrType.GetTypeInfo().DeclaredProperties)
            {
                var columnType = propertyInfo.Name;

                if (columnType.EndsWith("Max"))
                {
                    columnType = columnType.Substring(0, columnType.IndexOf("Max")) + "(max)";
                }

                columnType = columnType.Replace('_', ' ');

                entityType.GetOrAddProperty(propertyInfo).Relational().ColumnType = columnType;
            }
        }

        private static void MapSizedColumnTypes<TEntity>(ModelBuilder modelBuilder) where TEntity : class
        {
            var entityType = modelBuilder.Entity<TEntity>().Metadata;

            foreach (var propertyInfo in entityType.ClrType.GetTypeInfo().DeclaredProperties.Where(p => p.Name != "Id"))
            {
                entityType.GetOrAddProperty(propertyInfo).Relational().ColumnType = propertyInfo.Name.Replace('_', ' ') + "(3)";
            }
        }

        private static void MapPreciseColumnTypes<TEntity>(ModelBuilder modelBuilder) where TEntity : class
        {
            var entityType = modelBuilder.Entity<TEntity>().Metadata;

            foreach (var propertyInfo in entityType.ClrType.GetTypeInfo().DeclaredProperties.Where(p => p.Name != "Id"))
            {
                entityType.GetOrAddProperty(propertyInfo).Relational().ColumnType = propertyInfo.Name.Replace('_', ' ') + "(5, 2)";
            }
        }

        public override void Dispose() => _testStore.Dispose();

        public override bool SupportsBinaryKeys => true;

        public override DateTime DefaultDateTime => new DateTime();
    }

    public class MappedDataTypes
    {
        public byte Tinyint { get; set; }
        public short Smallint { get; set; }
        public int Int { get; set; }
        public long Bigint { get; set; }
        public float Real { get; set; }
        public double Double_precision { get; set; }
        public decimal Decimal { get; set; }
        public decimal Numeric { get; set; }

        public string Text { get; set; }
        public byte[] Bytea { get; set; }

        public DateTime Timestamp { get; set; }
        //public DateTime Timestamptz { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        //public DateTimeOffset Timetz { get; set; }
        public TimeSpan Interval { get; set; }

        public Guid Uuid { get; set; }
        public bool Bool { get; set; }

        // Types supported only on PostgreSQL
        public PhysicalAddress Macaddr { get; set; }
        public NpgsqlPoint Point { get; set; }
        public string Jsonb { get; set; }
        public Dictionary<string, string> Hstore { get; set; }

        // Composite
        //public SomeComposite SomeComposite { get; set; }

        // Array
        public int[] PrimitiveArray { get; set; }
        public PhysicalAddress[] NonPrimitiveArray { get; set; }

        public uint Xid { get; set; }
    }

    public class MappedSizedDataTypes
    {
        public int Id { get; set; }
        /*
        public string Char { get; set; }
        public string Character { get; set; }
        public string Varchar { get; set; }
        public string Char_varying { get; set; }
        public string Character_varying { get; set; }
        public string Nchar { get; set; }
        public string National_character { get; set; }
        public string Nvarchar { get; set; }
        public string National_char_varying { get; set; }
        public string National_character_varying { get; set; }
        public byte[] Binary { get; set; }
        public byte[] Varbinary { get; set; }
        public byte[] Binary_varying { get; set; }
        */
    }

    public class MappedScaledDataTypes
    {
        public int Id { get; set; }
        /*
        public float Float { get; set; }
        public float Double_precision { get; set; }
        public DateTimeOffset Datetimeoffset { get; set; }
        public DateTime Datetime2 { get; set; }
        public decimal Decimal { get; set; }
        public decimal Dec { get; set; }
        public decimal Numeric { get; set; }
        */
    }

    public class MappedPrecisionAndScaledDataTypes
    {
        public int Id { get; set; }
        /*
        public decimal Decimal { get; set; }
        public decimal Dec { get; set; }
        public decimal Numeric { get; set; }
        */
    }

    public class MappedNullableDataTypes
    {
        public byte? Tinyint { get; set; }
        public short? Smallint { get; set; }
        public int? Int { get; set; }
        public long? Bigint { get; set; }
        public float? Real { get; set; }
        public double? Double_precision { get; set; }
        public decimal? Decimal { get; set; }
        public decimal? Numeric { get; set; }

        public string Text { get; set; }
        public byte[] Bytea { get; set; }

        public DateTime? Timestamp { get; set; }
        public DateTime? Timestamptz { get; set; }
        public DateTime? Date { get; set; }
        public TimeSpan? Time { get; set; }
        public DateTimeOffset? Timetz { get; set; }
        public TimeSpan? Interval { get; set; }

        public Guid? Uuid { get; set; }
        public bool? Bool { get; set; }

        // Types supported only on PostgreSQL
        public PhysicalAddress Macaddr { get; set; }
        public NpgsqlPoint? Point { get; set; }
        public string Jsonb { get; set; }
        public Dictionary<string, string> Hstore { get; set; }

        // Composite
        //public SomeComposite SomeComposite { get; set; }

        // Array
        public int[] PrimitiveArray { get; set; }
        public PhysicalAddress[] NonPrimitiveArray { get; set; }

        public uint? Xid { get; set; }
    }

    /*
    public class SomeComposite
    {
        public int SomeNumber { get; set; }
        public string SomeText { get; set; }

        public override bool Equals(object obj)
        {
            var o = obj as SomeComposite;
            return o != null && o.SomeNumber == SomeNumber && o.SomeText == o.SomeText;
        }

        public override int GetHashCode() => SomeNumber.GetHashCode() ^ SomeText.GetHashCode();
    }
    */
}
