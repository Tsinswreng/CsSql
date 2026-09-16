using Microsoft.Extensions.DependencyInjection;

namespace Tsinswreng.CsSql.Test;

/// <summary>
/// CsSql 測試的全局依賴注入管理。
/// 整個 CsSql 測試項目的唯一根源 SvcProvider,由 exe 入口(Test.Sqlite/Test.Postgres)在組裝容器後賦值,
/// 所有 tester 通過 GetRSvc 直接取依賴,不再使用構造函數注入。
/// </summary>
public static class Di{
	/// <summary>根源服務提供器,入口在 InitSvc 的 BuildSvcProvider 回調中賦值。</summary>
	public static IServiceProvider SvcProvider{
		get{ return field ?? throw new InvalidOperationException("Di.SvcProvider has not been initialized."); }
		set{ field = value ?? throw new ArgumentNullException(nameof(value)); }
	}

	/// <summary>從根源容器解析服務,供 tester 字段初始化直接取依賴。</summary>
	public static T GetRSvc<T>() where T:class{
		return SvcProvider.GetRequiredService<T>();
	}
}