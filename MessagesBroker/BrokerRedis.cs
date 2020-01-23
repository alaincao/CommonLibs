//
// CommonLibs/MessagesBroker/BrokerLocal.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2020 Alain CAO
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using StackExchange.Redis;

using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;

	public class BrokerRedis : BrokerBase
	{
		public ConnectionMultiplexer	Connection						{ get; }
		public string					KeysPrefix						{ get; } = "";

		public BrokerRedis(string connectionString, string keysPrefix=null)
		{
			ASSERT( !string.IsNullOrWhiteSpace(connectionString), $"Missing parameter '{nameof(connectionString)}'" );

			Connection = ConnectionMultiplexer.Connect( connectionString );
			KeysPrefix = (keysPrefix == null) ? KeysPrefix : keysPrefix;

			LaunchSubscribe();
		}

		private void LaunchSubscribe()
		{
			var keySpacePrefix = $"__keyspace@0__:{KeysPrefix}";

			var subscriber = Connection.GetSubscriber();
			subscriber.Subscribe( keySpacePrefix+"*", async (c,v)=>
				{
					try
					{
						var value = (string)v;
						if( value != "rpush" )
							// Not an "add to list" operation
							return;
						var channel = (string)c;
						var endPointID = channel.Substring( keySpacePrefix.Length );

						await CheckPendingMessages( endPointID );
					}
					catch( System.Exception ex )
					{
						FAIL( $"Redis subscription to '{v}' threw an exception ({ex.GetType().FullName}): {ex.Message}" );
					}
				} );
		}

		protected override async Task SaveMessages(string endPointID, List<TMessage> messages)
		{
			ASSERT( !string.IsNullOrWhiteSpace(endPointID), $"Missing parameter '{nameof(endPointID)}'" );
			ASSERT( (messages != null) && (messages.Count > 0), $"Missing parameter '{nameof(messages)}'" );
			var key = $"{KeysPrefix}{endPointID}";
			var json = messages.ToJSON();
			var db = Connection.GetDatabase();

			var tr = db.CreateTransaction();
			var t_lrp = tr.ListRightPushAsync( key, json );
			var t_ke = tr.KeyExpireAsync( key, new TimeSpan(hours:0, minutes:0, seconds:MessagesExpireSeconds) );

			var rc = await tr.ExecuteAsync();
			ASSERT( rc, $"Failed to push messages' JSON to Redis' list" );
			await Task.WhenAll( new Task[]{ t_lrp, t_ke } );
		}

		/// <returns>The non-empty list of pending messages for this endpoint ; 'null' if none were found</returns>
		protected override async Task<List<TMessage>> RestoreMessages(string endPointID)
		{
			ASSERT( ! string.IsNullOrWhiteSpace(endPointID), $"Missing parameter '{nameof(endPointID)}" );
			var key = $"{KeysPrefix}{endPointID}";
			var db = Connection.GetDatabase();

			var tr = db.CreateTransaction();
			var t_lr = tr.ListRangeAsync( key );
			var t_d = tr.KeyDeleteAsync( key );

			var rc = await tr.ExecuteAsync();
			ASSERT( rc, $"Failed to get messages' from Redis'" );
			await Task.WhenAll( new Task[]{ t_d } );

			var jsons = await t_lr;
			var list = new List<TMessage>();
			foreach( var json in jsons )
			{
				var l = json.ToString().FromJSON<List<Dictionary<string,object>>>();
				foreach( var dict in l )
					list.Add( NewMessage(dict) );
			}

			if( list.Count == 0 )
				return null;
			return list;
		}
	}
}
