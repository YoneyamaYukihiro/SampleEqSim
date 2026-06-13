using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Secs4Net;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// secs4net の DI 登録ヘルパー。
/// secs4net 本体パッケージ (v2.4.4) には DI 拡張が含まれないため、
/// 公式サンプル (samples/DeviceWorkerService/ServiceProvider.cs) と同じ登録を提供する。
/// appsettings.json の "secs4net" セクションを <see cref="SecsGemOptions"/> にバインドする。
/// </summary>
public static class Secs4NetServiceCollectionExtensions
{
    public static IServiceCollection AddSecs4Net<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLogger>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TLogger : class, ISecsGemLogger
    {
        var configSection = configuration.GetSection("secs4net");
        services.Configure<SecsGemOptions>(configSection);
        services.AddSingleton<ISecsConnection, HsmsConnection>();
        services.AddSingleton<ISecsGem, SecsGem>();
        services.AddSingleton<ISecsGemLogger, TLogger>();
        return services;
    }
}
