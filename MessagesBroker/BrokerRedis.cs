//
// CommonLibs/MessagesBroker/BrokerLocal.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2020 Alain CAO
//
// Permission to use, copy, modify, and/or distribute this software for any
// purpose with or without fee is hereby granted.
// 
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
// REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
// INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
// LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
// OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
// PERFORMANCE OF THIS SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using StackExchange.Redis;

using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;
	using CMessage = Dictionary<string,object>;

	public class BrokerRedis : BrokerBase
	{
		public ConnectionMultiplexer	Connection						{ get; private set; }
		public string					KeysPrefix						{ get; } = "";

		/// <summary>Update to change the default serialization method (JSON) of the messages in the Redis database</summary>
		/// <remarks>'DeserializeMessages' must be updated accordingly</remarks>
		public Func<List<TMessage>,string>	SerializeMessages			{ get; set; }
		/// <summary>Update to change the default deserialization method (JSON) of the messages in the Redis database</summary>
		/// <remarks>'SerializeMessages' must be updated accordingly</remarks>
		public Func<string,List<CMessage>>	DeserializeMessages			{ get; set; }

		public BrokerRedis(string keysPrefix=null)
		{
			KeysPrefix = (keysPrefix == null) ? KeysPrefix : keysPrefix;

			SerializeMessages	= (messages)=>messages.ToJSON();
			DeserializeMessages	= (str)=>str.FromJSON<List<CMessage>>();
		}

		public Task Start(string connectionString)
		{
			ASSERT( !string.IsNullOrWhiteSpace(connectionString), $"Missing parameter '{nameof(connectionString)}'" );

			Connection = ConnectionMultiplexer.Connect( connectionString );

			LaunchSubscribe();
			return Task.CompletedTask;
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
			var value = SerializeMessages( messages );
			var db = Connection.GetDatabase();

			var tr = db.CreateTransaction();
			var t_lrp = tr.ListRightPushAsync( key, value );
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

			var values = await t_lr;
			var list = new List<TMessage>();
			foreach( string str in values )
			{
				var l = DeserializeMessages( str );
				foreach( var dict in l )
					list.Add( NewMessage(dict) );
			}

			if( list.Count == 0 )
				return null;
			return list;
		}
	}
}
