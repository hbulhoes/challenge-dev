﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using DriverCatalogService.Models;
using Microsoft.Extensions.Configuration;

namespace DriverCatalogService.Infrastructure
{
    public class DynamoDBRepository : IRepository
    {
        private readonly string _targetTableName;
        private DynamoDBContext _ddbContext;
        private RegionEndpoint _region;

        public DynamoDBRepository(IConfiguration configuration)
        {
            _targetTableName = configuration["Repository:TableName"];
            _region = RegionEndpoint.GetBySystemName(configuration["AWS:Region"]);

            AWSConfigsDynamoDB.Context.TypeMappings[typeof(Driver)] = new Amazon.Util.TypeMapping(typeof(Driver), _targetTableName);

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            _ddbContext = new DynamoDBContext(new AmazonDynamoDBClient(_region), config);
        }

        public async Task SetupTable()
        {
            using (var client = new AmazonDynamoDBClient(_region))
            {
                CreateTableRequest request = new CreateTableRequest
                {
                    TableName = _targetTableName,
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 2,
                        WriteCapacityUnits = 2
                    },
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement
                        {
                            KeyType = KeyType.HASH,
                            AttributeName = nameof(Driver.Id)
                        }
                    },
                    AttributeDefinitions = new List<AttributeDefinition>
                    {
                        new AttributeDefinition
                        {
                            AttributeName = nameof(Driver.Id),
                            AttributeType = ScalarAttributeType.S
                        }
                    }
                };

                await client.CreateTableAsync(request);

                var describeRequest = new DescribeTableRequest {TableName = _targetTableName};
                DescribeTableResponse response = null;
                do
                {
                    Thread.Sleep(1000);
                    response = await client.DescribeTableAsync(describeRequest);
                } while (response.Table.TableStatus != TableStatus.ACTIVE);
            }
        }

        public void DropTable()
        {
            using (var client = new AmazonDynamoDBClient(_region))
            {
                client.DeleteTableAsync(_targetTableName).Wait();
            }
        }

        public void Save(Driver driver)
        {
            var table = _ddbContext.GetTargetTable<Driver>();

            var doc = _ddbContext.ToDocument(driver);
            if (driver.ModifiedAt == null)
            {
                doc[nameof(Driver.ModifiedAt)] = DynamoDBNull.Null;
                table.PutItemAsync(doc).Wait();
            }
            else
            {
                table.UpdateItemAsync(doc).Wait();
            }
        }

        public Driver Load(string driverId)
        {
            var table = _ddbContext.GetTargetTable<Driver>();
            var doc = table.GetItemAsync(driverId).Result;
            if (doc != null)
            {
                return InstantiateDriver(doc);
            }

            return null;
        }

        private static Driver InstantiateDriver(Document doc)
        {
            var res = new Driver
            {
                Id = doc[nameof(Driver.Id)],
                CreatedAt = DateTime.Parse(doc[nameof(Driver.CreatedAt)]),
                ModifiedAt = !Equals(doc[nameof(Driver.ModifiedAt)], DynamoDBNull.Null) ? DateTime.Parse(doc[nameof(Driver.ModifiedAt)]) : (DateTime?) null
            };

            if (doc.ContainsKey(nameof(Driver.Name)))
            {
                var nameDoc = doc[nameof(Driver.Name)].AsDocument();
                res.Name = new Name
                {
                    FirstName = nameDoc[nameof(Name.FirstName)],
                    LastName = nameDoc[nameof(Name.LastName)]
                };
            }

            if (doc.ContainsKey(nameof(Driver.Address)))
            {
                var addressDoc = doc[nameof(Driver.Address)].AsDocument();
                res.Address = new Address
                {
                    FullAddress = addressDoc[nameof(Address.FullAddress)].AsString(),
                    Longitude = addressDoc.ContainsKey(nameof(Address.Longitude))? addressDoc[nameof(Address.Longitude)].AsString() : null,
                    Latitude = addressDoc.ContainsKey(nameof(Address.Latitude))? addressDoc[nameof(Address.Latitude)].AsString() : null
                };
            }

            if (doc.ContainsKey(nameof(Driver.Car)))
            {
                var carDoc = doc[nameof(Driver.Car)].AsDocument();
                res.Car = new Car
                {
                    Maker = carDoc[nameof(Car.Maker)],
                    Model = carDoc[nameof(Car.Model)],
                    LicensePlate = carDoc[nameof(Car.LicensePlate)]
                };
            }

            return res;
        }

        public bool Exists(Name driverName)
        {
            var request = new ScanRequest
            {
                TableName = _targetTableName,
                //AttributesToGet = new List<string> {nameof(Driver.Id)},
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":firstName", new AttributeValue { S = driverName.FirstName}},
                    {":lastName", new AttributeValue { S = driverName.LastName}}
                },
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#n", "Name"}
                },
                FilterExpression = "#n.FirstName = :firstName and #n.LastName = :lastName",
                Limit = 1
            };

            using (var client = new AmazonDynamoDBClient(_region))
            {
                var response = client.ScanAsync(request).Result;
                return response.Count > 0;
            }
        }

        public bool ContainsAnother(string driverId, Name driverName)
        {
            var table = _ddbContext.GetTargetTable<Driver>();
            var filter = new ScanFilter();
            filter.AddCondition($"{nameof(Driver.Name)}.{nameof(Name.FirstName)}", ScanOperator.Equal, driverName.FirstName);
            filter.AddCondition($"{nameof(Driver.Name)}.{nameof(Name.LastName)}", ScanOperator.Equal, driverName.LastName);
            filter.AddCondition(nameof(Driver.Id), ScanOperator.NotEqual, driverId);

            var search = table.Scan(filter);
            return search.GetNextSetAsync().Result.Any();
        }

        public void Delete(string driverId)
        {
            _ddbContext.DeleteAsync<Driver>(driverId).Wait();
        }

        public Driver[] List(string sortByField, string sortOrder)
        {
            var table = _ddbContext.GetTargetTable<Driver>();
            var search = table.Scan(new ScanFilter());

            var docs = search.GetNextSetAsync().Result;

            string KeySelector(Document d)
            {
                var n = d[nameof(Driver.Name)].AsDocument();
                return n[sortByField].AsString();
            }

            docs = sortOrder == "asc" ? docs.OrderBy(KeySelector).ToList() : docs.OrderByDescending(KeySelector).ToList();

            return docs.Select(InstantiateDriver).ToArray();
        }
    }
}