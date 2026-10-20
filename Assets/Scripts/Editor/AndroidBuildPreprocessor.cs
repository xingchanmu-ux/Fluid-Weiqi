using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Automatically configures Android-specific player settings before building.
// This ensures the build works in CI even if ProjectSettings.asset was not
// manually switched to the Android platform.
public class AndroidBuildPreprocessor : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if(report.summary.platform != BuildTarget.Android)
			return;

		const string bundleId = "com.fluidweiqi.app";

		PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, bundleId);
		PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
		PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
		PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

		string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
		if(string.IsNullOrEmpty(defines))
			defines = "DISABLESTEAMWORKS";
		else if(!defines.Contains("DISABLESTEAMWORKS"))
			defines += ";DISABLESTEAMWORKS";
		PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defines);
	}
}
