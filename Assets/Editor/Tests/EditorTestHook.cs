using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Runs EditMode tests in the open editor when /tmp/juicy-run-tests exists.
// Results go to /tmp/juicy-tests.txt. Batch mode can't run while the editor
// holds the project open, so agents use this instead.
[InitializeOnLoad]
public static class EditorTestHook
{
    const string Flag = "/tmp/juicy-run-tests";
    const string Report = "/tmp/juicy-tests.txt";
    static bool running;

    static EditorTestHook()
    {
        EditorApplication.update += Watch;
    }

    static void Watch()
    {
        if (running || !File.Exists(Flag)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        File.Delete(Flag);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks(api));
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    class Callbacks : ICallbacks
    {
        readonly TestRunnerApi api;
        readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

        public Callbacks(TestRunnerApi api) { this.api = api; }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            log.AppendLine(result.TestStatus + " " + result.Test.FullName + (result.TestStatus == TestStatus.Failed ? "\n    " + result.Message : ""));
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            log.Insert(0, "passed " + result.PassCount + " failed " + result.FailCount + " skipped " + result.SkipCount + "\n");
            File.WriteAllText(Report, log.ToString());
            api.UnregisterCallbacks(this);
            running = false;
        }
    }
}
