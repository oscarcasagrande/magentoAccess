﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetOpenAuth.Messaging;
using MagentoAccess;
using MagentoAccess.Misc;
using MagentoAccess.Models.CreateOrders;
using MagentoAccess.Models.CreateProducts;
using MagentoAccess.Models.Credentials;
using MagentoAccess.Models.DeleteProducts;
using MagentoAccess.Models.GetOrders;
using MagentoAccess.Models.GetProducts;
using MagentoAccess.Services.Soap._1_9_2_1_ce;
using Netco.Extensions;
using Netco.Logging;
using Netco.Logging.NLogIntegration;
using NUnit.Framework;

namespace MagentoAccessTestsIntegration.TestEnvironment
{
	internal partial class BaseTest
	{
		protected ConcurrentDictionary< string, List< Order > > _orders;
		protected ConcurrentDictionary< string, Dictionary< int, string > > _productsIds;
		protected MagentoServiceLowLevelSoap_v_1_9_2_1_ce _magentoLowLevelSoapForCreatingTestEnvironment;

		protected IMagentoService CreateMagentoService( string apiUser, string apiKey, string accessToken, string accessTokenSecret, string consumerKey, string consumerSecret, string magentoBaseUrl, string requestTokenUrl, string authorizeUrl, string accessTokenUrl, string magentoVersionByDefault, int getProductsMaxThreads, int sessionLifeTime, bool supressExc, ThrowExceptionIfFailed onUpdateInventory = ThrowExceptionIfFailed.OneItem )
		{
			var magentoService = new MagentoService( new MagentoAuthenticatedUserCredentials(
				accessToken,
				accessTokenSecret,
				magentoBaseUrl,
				consumerSecret,
				consumerKey,
				apiUser,
				apiKey,
				getProductsMaxThreads,
				sessionLifeTime,
				true
			), string.IsNullOrWhiteSpace( magentoVersionByDefault ) ? null : new MagentoConfig() { VersionByDefault = magentoVersionByDefault, OnUpdateInventory = onUpdateInventory } );
			magentoService.InitAsync( supressExc ).Wait();
			return magentoService;
		}

		[ SetUp ]
		public void Setup()
		{
			NetcoLogger.LoggerFactory = new NLogLoggerFactory();
		}

		[ TestFixtureSetUp ]
		public void TestFixtureSetup()
		{
		}

		[ TestFixtureTearDown ]
		public void TestFixtureTearDown()
		{
			//this.DeleteProducts();
		}

		protected void CreateOrders()
		{
			var testStoresCredentials = this.GetTestStoresCredentials();

			Action< MagentoServiceSoapCredentials > createOrdersAction = credentials =>
			{
				try
				{
					var ordersModels = new List< CreateOrderModel >();
					for( var i = 0; i < 5; i++ )
					{
						ordersModels.Add( new CreateOrderModel() { StoreId = "0", CustomerFirstName = "max", CustomerMail = "qwe@qwe.com", CustomerLastName = "kits", ProductIds = this._productsIds[ credentials.StoreUrl ].Keys.Select( x => x.ToString() ) } );
					}

					var magentoService = this.CreateMagentoService( credentials.SoapApiUser, credentials.SoapApiKey, "null", "null", "null", "null", credentials.StoreUrl, "http://w.com", "http://w.com", "http://w.com", credentials.MagentoVersion, credentials.GetProductsThreadsLimit, credentials.SessionLifeTimeMs, false, ThrowExceptionIfFailed.AllItems );
					var creationResult = magentoService.CreateOrderAsync( ordersModels );
					creationResult.Wait();
					var ordersIds = creationResult.Result.Select( x => x.OrderId ).ToList();
					var ordersTask = magentoService.GetOrdersAsync( ordersIds );
					ordersTask.Wait();

					if( this._orders == null )
						this._orders = new ConcurrentDictionary< string, List< Order > >();

					this._orders[ credentials.StoreUrl ] = new List< Order >();
					var ordersToAdd = ordersTask.Result.ToList().OrderBy( x => x.UpdatedAt );
					this._orders[ credentials.StoreUrl ].AddRange( ordersToAdd );
				}
				catch
				{
					// ignored
				}
			};

			var magentoServiceSoapCredentialses = testStoresCredentials as IList< MagentoServiceSoapCredentials > ?? testStoresCredentials.ToList();
			magentoServiceSoapCredentialses.DoInBatchAsync( magentoServiceSoapCredentialses.Count(), async x =>
			{
				await Task.Run( () => createOrdersAction( x ) ).ConfigureAwait(false);
			} ).Wait();
		}

		protected void CreateProducts()
		{
			try
			{
				this._productsIds = new ConcurrentDictionary< string, Dictionary< int, string > >(); // new Dictionary< int, string >();

				var testStoresCredentials = this.GetTestStoresCredentials();

				Action< MagentoServiceSoapCredentials > createProductAction = credentials =>
				{
					try
					{
						var source = new List< CreateProductModel >();
						for( var i = 0; i < 5; i++ )
						{
							var tiks = DateTime.UtcNow.Ticks.ToString();
							var sku = $"TddTestSku{i}_{tiks}";
							var name = $"TddTestName{i}_{tiks}";

							source.Add( new CreateProductModel( "0", sku, name, 1, i == 4 ? "bundle" : "simple" ) );
						}
						var magentoService = this.CreateMagentoService( credentials.SoapApiUser, credentials.SoapApiKey, "null", "null", "null", "null", credentials.StoreUrl, "http://w.com", "http://w.com", "http://w.com", credentials.MagentoVersion, credentials.GetProductsThreadsLimit, credentials.SessionLifeTimeMs, false, ThrowExceptionIfFailed.AllItems );
						var creationResult = magentoService.CreateProductAsync( source );

						creationResult.Wait();
						this._productsIds[ credentials.StoreUrl ] = new Dictionary< int, string >();
						this._productsIds[ credentials.StoreUrl ].AddRange( creationResult.Result.ToDictionary( x => x.Result, y => y.Sku ) );
					}
					catch
					{
						// ignored
					}
				};

				var magentoServiceSoapCredentialses = testStoresCredentials as IList< MagentoServiceSoapCredentials > ?? testStoresCredentials.ToList();
				magentoServiceSoapCredentialses.DoInBatchAsync( magentoServiceSoapCredentialses.Count(), async x =>
				{
					await Task.Run( () => createProductAction( x ) ).ConfigureAwait(false);
				} ).Wait();
			}
			catch
			{
				// ignored
			}
		}

		protected void DeleteProducts()
		{
			try
			{
				var testStoresCredentials = this.GetTestStoresCredentials();

				foreach( var credentials in testStoresCredentials )
				{
					var magentoService = this.CreateMagentoService( credentials.SoapApiUser, credentials.SoapApiKey, "null", "null", "null", "null", credentials.StoreUrl, "http://w.com", "http://w.com", "http://w.com", credentials.MagentoVersion, credentials.GetProductsThreadsLimit, credentials.SessionLifeTimeMs, false, ThrowExceptionIfFailed.AllItems );

					var productsToRemove = this.GetOnlyProductsCreatedForThisTests( magentoService );
					var productsToRemoveDeleteProductModels = productsToRemove.Select( p => new DeleteProductModel( "0", 0, p.ProductId, "" ) ).ToList();

					var deleteres = magentoService.DeleteProductAsync( productsToRemoveDeleteProductModels );

					deleteres.Wait();
				}
			}
			catch( Exception exception )
			{
				Console.WriteLine( "Can't delete products {0}", exception );
			}
		}

		protected IEnumerable< Product > GetOnlyProductsCreatedForThisTests( MagentoServiceSoapCredentials magentoServiceSoapCredentials )
		{
			var magentoService = this.CreateMagentoService( magentoServiceSoapCredentials.SoapApiUser, magentoServiceSoapCredentials.SoapApiKey, "null", "null", "null", "null", magentoServiceSoapCredentials.StoreUrl, "http://w.com", "http://w.com", "http://w.com", MagentoVersions.M_2_0_2_0, magentoServiceSoapCredentials.GetProductsThreadsLimit, magentoServiceSoapCredentials.SessionLifeTimeMs, false, ThrowExceptionIfFailed.AllItems );
			var getProductsTask = magentoService.GetProductsAsync( new[] { 0, 1 } );
			getProductsTask.Wait();

			var allProductsinMagent = getProductsTask.Result.ToList();
			var onlyProductsCreatedForThisTests = allProductsinMagent.Where( x => this._productsIds[ magentoServiceSoapCredentials.StoreUrl ].ContainsKey( int.Parse( x.ProductId ) ) );
			return onlyProductsCreatedForThisTests;
		}

		protected IEnumerable< Product > GetOnlyProductsCreatedForThisTests( IMagentoService service )
		{
			var getProductsTask = service.GetProductsAsync( new[] { 0, 1 } );
			getProductsTask.Wait();

			var allProductsinMagent = getProductsTask.Result.ToList();
			var onlyProductsCreatedForThisTests = allProductsinMagent.Where( x => x.Sku.StartsWith( "testsku", StringComparison.InvariantCultureIgnoreCase ) && x.ProductType == "simple" ).TakeWhile( ( x, i ) => i < 5 );
			return onlyProductsCreatedForThisTests;
		}

		internal class MagentoServiceSoapCredentials
		{
			public string SoapApiUser { get; set; }
			public string SoapApiKey { get; set; }
			public string StoreUrl { get; set; }
			public string MagentoVersion { get; set; }
			public int GetProductsThreadsLimit { get; set; }
			public int SessionLifeTimeMs { get; set; }

			public override string ToString()
			{
				return this.StoreUrl.ToString();
			}
		}
	}
}