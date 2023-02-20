using EFCore.BulkExtensions.SQLAdapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace EFCore.BulkExtensions.Tests
{
    public class EFCoreBulkTest
    {
        protected int EntitiesNumber => 10000;

       // private static Func<TestContext, int> ItemsCountQuery = EF.CompileQuery<TestContext, int>(ctx => ctx.Items.Count());
        //private static Func<TestContext, Item> LastItemQuery = EF.CompileQuery<TestContext, Item>(ctx => ctx.Items.LastOrDefault());
        //private static Func<TestContext, IEnumerable<Item>> AllItemsQuery = EF.CompileQuery<TestContext, IEnumerable<Item>>(ctx => ctx.Items.AsNoTracking());

        [Theory]
        [InlineData(DbServerType.SQLServer, true)]
        [InlineData(DbServerType.SQLite, true)]
        //[InlineData(DbServerType.SQLServer, false)] // for speed comparison with Regular EF CUD operations
        public void OperationsTest(DbServerType dbServer, bool isBulk)
        {
            ContextUtil.DbServer = dbServer;

           // DeletePreviousDatabase();
            //new EFCoreBatchTest().RunDeleteAll(dbServer);

            RunInsert(isBulk);
            //RunInsertOrUpdate(isBulk, dbServer);
            //RunUpdate(isBulk, dbServer);

            //RunRead(isBulk);

            //if (dbServer == DbServerType.SQLServer)
            //{
            //    RunInsertOrUpdateOrDelete(isBulk); // Not supported for Sqlite (has only UPSERT), instead use BulkRead, then split list into sublists and call separately Bulk methods for Insert, Update, Delete.
            //}
            //RunDelete(isBulk, dbServer);

            //CheckQueryCache();
        }

        [Theory]
        [InlineData(DbServerType.MySQL, true)]
        //[InlineData(DbServerType.SQLServer, false)] // for speed comparison with Regular EF CUD operations
        public void OperationsMySqlTest(DbServerType dbServer, bool isBulk)
        {
            ContextUtil.DbServer = dbServer;

           // DeletePreviousDatabase();
            //new EFCoreBatchTest().RunDeleteAll(dbServer);

            RunInsert(isBulk);
            //RunInsertOrUpdate(isBulk, dbServer);
            //RunUpdate(isBulk, dbServer);

            //RunRead(isBulk);

            //if (dbServer == DbServerType.SQLServer)
            //{
            //    RunInsertOrUpdateOrDelete(isBulk); // Not supported for Sqlite (has only UPSERT), instead use BulkRead, then split list into sublists and call separately Bulk methods for Insert, Update, Delete.
            //}
            //RunDelete(isBulk, dbServer);

            //CheckQueryCache();
        }


        [Theory]
        [InlineData(DbServerType.SQLServer)]
        [InlineData(DbServerType.SQLite)]
        public void SideEffectsTest(DbServerType dbServer)
        {
            BulkOperationShouldNotCloseOpenConnection(dbServer, context => context.BulkInsert(new[] { new Item() }));
            BulkOperationShouldNotCloseOpenConnection(dbServer, context => context.BulkUpdate(new[] { new Item() }));
        }

        private static void BulkOperationShouldNotCloseOpenConnection(DbServerType dbServer, Action<TestContext> bulkOperation)
        {
            ContextUtil.DbServer = dbServer;
            using var context = new TestContext(ContextUtil.GetOptions());

            var sqlHelper = context.GetService<ISqlGenerationHelper>();
            context.Database.OpenConnection();

            try
            {
                // we use a temp table to verify whether the connection has been closed (and re-opened) inside BulkUpdate(Async)
                var columnName = sqlHelper.DelimitIdentifier("Id");
                var tableName = sqlHelper.DelimitIdentifier("#MyTempTable");
                var createTableSql = $" TABLE {tableName} ({columnName} INTEGER);";

                createTableSql = dbServer switch
                {
                    DbServerType.SQLite => $"CREATE TEMPORARY {createTableSql}",
                    DbServerType.SQLServer => $"CREATE {createTableSql}",
                    _ => throw new ArgumentException($"Unknown database type: '{dbServer}'.", nameof(dbServer)),
                };

                context.Database.ExecuteSqlRaw(createTableSql);

                bulkOperation(context);

                context.Database.ExecuteSqlRaw($"SELECT {columnName} FROM {tableName}");
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }

        private void DeletePreviousDatabase()
        {
            using var context = new TestContext(ContextUtil.GetOptions());
            context.Database.EnsureDeleted();
        }

        private void CheckQueryCache()
        {
            using var context = new TestContext(ContextUtil.GetOptions());
            var compiledQueryCache = ((MemoryCache)context.GetService<IMemoryCache>());

            Assert.Equal(0, compiledQueryCache.Count);
        }

        private void WriteProgress(decimal percentage)
        {
            Debug.WriteLine(percentage);
        }

        private void RunInsert(bool isBulk)
        {
            using var context = new TestContext(ContextUtil.GetOptions());

            var entities1 = new List<Documents>();
            for (int i = 1; i <= 10; i++)
            {
                var entity = new Documents
                {
                    DocumentId = Guid.NewGuid(),
                    IsActive = true,
                    Content = i + "天下第e",
                    Tag = "tianxia" + i
                };
                entities1.Add(entity);
            }

            var entities2 = new List<Documents>();

            for (int i = 6; i <= 15; i++)
            {
                var entity = new Documents
                {
                    DocumentId = Guid.NewGuid(),
                    IsActive = true,
                    Content = i + "天下第e",
                    Tag = "tianxia" + i
                };
                entities2.Add(entity);
            }
            var entities3 = new List<Documents>();
            var entities4 = new List<Documents>();

            // INSERT

            for (int i = 0; i <= context.Documents.ToList().Count; i++)
            {
                entities4.Add(context.Documents.ToList()[i]);
                if (i > 8)
                {
                    break;
                }
            }
            context.BulkDelete(entities4);

            //context.BulkInsert(entities1);
            var fd = context.Documents.ToList();
            for (int i = 0; i < fd.Count - 10; i++)
            {
                fd[i].ContentLength = 10;
            }
            context.BulkInsertOrUpdate(fd);


            var entities = new List<Item>();
            //var subEntities = new List<ItemHistory>();
            //for (int i = 1, j = -(EntitiesNumber - 1); i < EntitiesNumber; i++, j++)
            //{
            //    var entity = new Item
            //    {
            //        ItemId = 0, //isBulk ? j : 0, // no longer used since order(Identity temporary filled with negative values from -N to -1) is set automaticaly with default config PreserveInsertOrder=TRUE
            //        Name = "name " + i,
            //        Description = "info " + Guid.NewGuid().ToString().Substring(0, 3),
            //        Quantity = i % 10,
            //        Price = i / (i % 5 + 1),
            //        TimeUpdated = DateTime.Now,
            //        ItemHistories = new List<ItemHistory>()
            //    };

            //    var subEntity1 = new ItemHistory
            //    {
            //        ItemHistoryId = SeqGuid.Create(),
            //        Remark = $"some more info {i}.1"
            //    };
            //    var subEntity2 = new ItemHistory
            //    {
            //        ItemHistoryId = SeqGuid.Create(),
            //        Remark = $"some more info {i}.2"
            //    };
            //    entity.ItemHistories.Add(subEntity1);
            //    entity.ItemHistories.Add(subEntity2);

            //    entities.Add(entity);
            //}

            //if (isBulk)
            //{
            //    if (ContextUtil.DbServer == DbServerType.SQLServer)
            //    {
            //        using var transaction = context.Database.BeginTransaction();
            //        var bulkConfig = new BulkConfig
            //        {
            //            //PreserveInsertOrder = true, // true is default
            //            SetOutputIdentity = true,
            //            BatchSize = 4000,
            //            UseTempDB = true,
            //            CalculateStats = true
            //        };
            //        context.BulkInsert(entities, bulkConfig, (a) => WriteProgress(a));
            //        Assert.Equal(EntitiesNumber - 1, bulkConfig.StatsInfo.StatsNumberInserted);
            //        Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberUpdated);
            //        Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberDeleted);

            //        foreach (var entity in entities)
            //        {
            //            foreach (var subEntity in entity.ItemHistories)
            //            {
            //                subEntity.ItemId = entity.ItemId; // setting FK to match its linked PK that was generated in DB
            //            }
            //            subEntities.AddRange(entity.ItemHistories);
            //        }
            //        context.BulkInsert(subEntities);

            //        transaction.Commit();
            //    }
            //    else if (ContextUtil.DbServer == DbServerType.SQLite)
            //    {
            //        using var transaction = context.Database.BeginTransaction();
            //        var bulkConfig = new BulkConfig() { SetOutputIdentity = true };
            //        context.BulkInsert(entities, bulkConfig);

            //        foreach (var entity in entities)
            //        {
            //            foreach (var subEntity in entity.ItemHistories)
            //            {
            //                subEntity.ItemId = entity.ItemId; // setting FK to match its linked PK that was generated in DB
            //            }
            //            subEntities.AddRange(entity.ItemHistories);
            //        }
            //        bulkConfig.SetOutputIdentity = false;
            //        context.BulkInsert(subEntities, bulkConfig);

            //        transaction.Commit();
            //    }
            //}
            //else
            //{
            //    context.Items.AddRange(entities);
            //    context.SaveChanges();
            //}

            //// TEST
            //int entitiesCount = ItemsCountQuery(context);
            //Item lastEntity = context.Items.OrderByDescending(a => a.ItemId).FirstOrDefault();

            //Assert.Equal(EntitiesNumber - 1, entitiesCount);
            //Assert.NotNull(lastEntity);
            //Assert.Equal("name " + (EntitiesNumber - 1), lastEntity.Name);
        }

        //private void RunInsertOrUpdate(bool isBulk, DbServerType dbServer)
        //{
        //    using var context = new TestContext(ContextUtil.GetOptions());

        //    var entities = new List<Item>();
        //    var dateTimeNow = DateTime.Now;
        //    for (int i = 2; i <= EntitiesNumber; i += 2)
        //    {
        //        entities.Add(new Item
        //        {
        //            ItemId = isBulk ? i : 0,
        //            Name = "name InsertOrUpdate " + i,
        //            Description = "info",
        //            Quantity = i + 100,
        //            Price = i / (i % 5 + 1),
        //            TimeUpdated = dateTimeNow
        //        });
        //    }
        //    if (isBulk)
        //    {
        //        var bulkConfig = new BulkConfig() { SetOutputIdentity = true, CalculateStats = true };
        //        context.BulkInsertOrUpdate(entities, bulkConfig, (a) => WriteProgress(a));
        //        if (dbServer == DbServerType.SQLServer)
        //        {
        //            Assert.Equal(1, bulkConfig.StatsInfo.StatsNumberInserted);
        //            Assert.Equal(EntitiesNumber / 2 - 1, bulkConfig.StatsInfo.StatsNumberUpdated);
        //            Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberDeleted);
        //        }
        //    }
        //    else
        //    {
        //        context.Items.Add(entities[entities.Count - 1]);
        //        context.SaveChanges();
        //    }

        //    // TEST
        //    int entitiesCount = context.Items.Count();
        //    Item lastEntity = context.Items.OrderByDescending(a => a.ItemId).FirstOrDefault();

        //    Assert.Equal(EntitiesNumber, entitiesCount);
        //    Assert.NotNull(lastEntity);
        //    Assert.Equal("name InsertOrUpdate " + EntitiesNumber, lastEntity.Name);
        //}

        //private void RunInsertOrUpdateOrDelete(bool isBulk)
        //{
        //    using var context = new TestContext(ContextUtil.GetOptions());

        //    var entities = new List<Item>();
        //    var dateTimeNow = DateTime.Now;
        //    for (int i = 2; i <= EntitiesNumber; i += 2)
        //    {
        //        entities.Add(new Item
        //        {
        //            ItemId = i,
        //            Name = "name InsertOrUpdateOrDelete " + i,
        //            Description = "info",
        //            Quantity = i,
        //            Price = i / (i % 5 + 1),
        //            TimeUpdated = dateTimeNow
        //        });
        //    }

        //    int? keepEntityItemId = null;
        //    if (isBulk)
        //    {
        //        var bulkConfig = new BulkConfig() { SetOutputIdentity = true, CalculateStats = true };

        //        keepEntityItemId = 3;
        //        bulkConfig.SetSynchronizeFilter<Item>(e => e.ItemId != keepEntityItemId.Value);
        //        context.BulkInsertOrUpdateOrDelete(entities, bulkConfig, (a) => WriteProgress(a));
        //        Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberInserted);
        //        Assert.Equal(EntitiesNumber / 2, bulkConfig.StatsInfo.StatsNumberUpdated);
        //        Assert.Equal((EntitiesNumber / 2) - 1, bulkConfig.StatsInfo.StatsNumberDeleted);
        //    }
        //    else
        //    {
        //        var existingItems = context.Items;
        //        var removedItems = existingItems.Where(x => !entities.Any(y => y.ItemId == x.ItemId));
        //        context.Items.RemoveRange(removedItems);
        //        context.Items.AddRange(entities);
        //        context.SaveChanges();
        //    }

        //    // TEST
        //    int entitiesCount = context.Items.Count();
        //    Item firstEntity = context.Items.OrderBy(a => a.ItemId).FirstOrDefault();
        //    Item lastEntity = context.Items.OrderByDescending(a => a.ItemId).FirstOrDefault();

        //    Assert.Equal((EntitiesNumber / 2) + (keepEntityItemId != null ? 1 : 0), entitiesCount);
        //    Assert.NotNull(firstEntity);
        //    Assert.Equal("name InsertOrUpdateOrDelete 2", firstEntity.Name);
        //    Assert.NotNull(lastEntity);
        //    Assert.Equal("name InsertOrUpdateOrDelete " + EntitiesNumber, lastEntity.Name);

        //    if (keepEntityItemId != null)
        //    {
        //        Assert.NotNull(context.Items.Where(x => x.ItemId == keepEntityItemId.Value).FirstOrDefault());
        //    }

        //    if (isBulk)
        //    {
        //        var bulkConfig = new BulkConfig() { SetOutputIdentity = true, CalculateStats = true };
        //        bulkConfig.SetSynchronizeFilter<Item>(e => e.ItemId != keepEntityItemId.Value);
        //        context.BulkInsertOrUpdateOrDelete(new List<Item>(), bulkConfig);

        //        var storedEntities = context.Items.ToList();
        //        Assert.Single(storedEntities);
        //        Assert.Equal(3, storedEntities[0].ItemId);
        //    }
        //}

        //private void RunUpdate(bool isBulk, DbServerType dbServer)
        //{
        //    using var context = new TestContext(ContextUtil.GetOptions());

        //    int counter = 1;
        //    var entities = context.Items.AsNoTracking().ToList();
        //    foreach (var entity in entities)
        //    {
        //        entity.Description = "Desc Update " + counter++;
        //        entity.Quantity += 1000; // will not be changed since Quantity property is not in config PropertiesToInclude
        //    }
        //    if (isBulk)
        //    {
        //        var bulkConfig = new BulkConfig
        //        {
        //            PropertiesToInclude = new List<string> { nameof(Item.Description) },
        //            UpdateByProperties = dbServer == DbServerType.SQLServer ? new List<string> { nameof(Item.Name) } : null,
        //            CalculateStats = true
        //        };
        //        context.BulkUpdate(entities, bulkConfig);
        //        if (dbServer == DbServerType.SQLServer)
        //        {
        //            Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberInserted);
        //            Assert.Equal(EntitiesNumber, bulkConfig.StatsInfo.StatsNumberUpdated);
        //            Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberDeleted);
        //        }
        //    }
        //    else
        //    {
        //        context.Items.UpdateRange(entities);
        //        context.SaveChanges();
        //    }

        //    // TEST
        //    int entitiesCount = context.Items.Count();
        //    Item lastEntity = context.Items.OrderByDescending(a => a.ItemId).FirstOrDefault();

        //    Assert.Equal(EntitiesNumber, entitiesCount);
        //    Assert.NotNull(lastEntity);
        //    Assert.Equal("name InsertOrUpdate " + EntitiesNumber, lastEntity.Name);
        //}

        //private void RunRead(bool isBulk)
        //{
        //    using var context = new TestContext(ContextUtil.GetOptions());

        //    var entities = new List<Item>();
        //    for (int i = 1; i < EntitiesNumber; i++)
        //    {
        //        var entity = new Item
        //        {
        //            Name = "name " + i,
        //        };
        //        entities.Add(entity);
        //    }

        //    context.BulkRead(
        //        entities,
        //        new BulkConfig
        //        {
        //            UpdateByProperties = new List<string> { nameof(Item.Name) }
        //        }
        //    );

        //    Assert.Equal(1, entities[0].ItemId);
        //    Assert.Equal(0, entities[1].ItemId);
        //    Assert.Equal(3, entities[2].ItemId);
        //    Assert.Equal(0, entities[3].ItemId);

        //    var entitiesHist = new List<ItemHistory>();
        //    entitiesHist.Add(new ItemHistory { Remark = "some more info 1.1" });
        //    var bulkConfigHist = new BulkConfig { UpdateByProperties = new List<string> { nameof(ItemHistory.Remark) } };
        //    context.BulkRead(entitiesHist, bulkConfigHist);

        //    var itemHistoryId = context.ItemHistories.Where(a => a.Remark == "some more info 1.1").FirstOrDefault().ItemHistoryId;
        //    Assert.Equal(itemHistoryId, entitiesHist[0].ItemHistoryId);
        //}

        //private void RunDelete(bool isBulk, DbServerType dbServer)
        //{
        //    using var context = new TestContext(ContextUtil.GetOptions());

        //    var entities = AllItemsQuery(context).ToList();
        //    // ItemHistories will also be deleted because of Relationship - ItemId (Delete Rule: Cascade)
        //    if (isBulk)
        //    {
        //        var bulkConfig = new BulkConfig() { CalculateStats = true };
        //        context.BulkDelete(entities, bulkConfig);
        //        if (dbServer == DbServerType.SQLServer)
        //        {
        //            Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberInserted);
        //            Assert.Equal(0, bulkConfig.StatsInfo.StatsNumberUpdated);
        //            Assert.Equal(entities.Count, bulkConfig.StatsInfo.StatsNumberDeleted);
        //        }
        //    }
        //    else
        //    {
        //        context.Items.RemoveRange(entities);
        //        context.SaveChanges();
        //    }

        //    // TEST
        //    int entitiesCount = context.Items.Count();
        //    Item lastEntity = context.Items.OrderByDescending(a => a.ItemId).FirstOrDefault();

        //    Assert.Equal(0, entitiesCount);
        //    Assert.Null(lastEntity);

        //    // RESET AutoIncrement
        //    string deleteTableSql = dbServer switch
        //    {
        //        DbServerType.SQLServer => $"DBCC CHECKIDENT('[dbo].[{nameof(Item)}]', RESEED, 0);",
        //        DbServerType.SQLite => $"DELETE FROM sqlite_sequence WHERE name = '{nameof(Item)}';",
        //        _ => throw new ArgumentException($"Unknown database type: '{dbServer}'.", nameof(dbServer)),
        //    };
        //    context.Database.ExecuteSqlRaw(deleteTableSql);
        //}
    }
}
