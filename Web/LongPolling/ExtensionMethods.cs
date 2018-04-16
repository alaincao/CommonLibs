using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

namespace CommonLibs.Web.LongPolling
{
	public static class ExtensionMethods
	{
		public static void AddMessageHandlerSingleton(this IServiceCollection services, Func<IServiceProvider,MessageHandler> messageHandlerFactory, Func<IServiceProvider,MessageContextService> messageContextServiceFactory=null)
		{
			services.AddSingleton<MessageHandler>( messageHandlerFactory );

			if( messageContextServiceFactory == null )
				services.AddScoped<MessageContextService>();
			else
				services.AddScoped<MessageContextService>( messageContextServiceFactory );
		}
	}
}
