using InputAutomationEngine;

// ---------------------------------------------------------------------------------------
// Input Abstraction & Workflow Automation Engine
//
// Driver-free: keyboard interception uses a user-space WH_KEYBOARD_LL hook and Xbox
// controller input uses XInput polling. No kernel drivers, no DPI/sensor manipulation.
// ---------------------------------------------------------------------------------------

var profilesPath = Path.Combine(AppContext.BaseDirectory, "profiles.json");

var profileManager = new ProfileManager(profilesPath);
var actionExecutor = new ActionExecutor();

// The currently active profile, kept in sync by ProcessMonitor and read by the keyboard
// hook callback and the controller poll thread. All three run on different threads, so
// access goes through Volatile to publish/observe the reference safely without a lock.
Profile activeProfile = profileManager.GetActiveProfile("*");

// VK codes we suppressed on key-down, so the matching key-up is suppressed too even if
// the active profile changes mid-press.
var suppressedKeysDown = new HashSet<int>();
var suppressLock = new object();

var processMonitor = new ProcessMonitor();
var keyboardHook = new KeyboardHook();
var controllerEngine = new XboxControllerEngine();

processMonitor.ActiveProcessChanged += (_, e) =>
{
    var profile = profileManager.GetActiveProfile(e.ExeName);
    Volatile.Write(ref activeProfile, profile);
    Console.WriteLine($"[Program] Foreground app: '{e.ExeName}' -> profile target '{profile.ProcessTarget}' " +
                       $"({profile.KeyMappings.Count} key mapping(s), {profile.ControllerMappings.Count} controller mapping(s)).");
};

keyboardHook.KeyEvent += (_, e) =>
{
    var profile = Volatile.Read(ref activeProfile);

    if (e.IsKeyDown)
    {
        var mapping = FindKeyMapping(profile, e.VkCode);
        if (mapping is null) return;

        e.Suppress = mapping.Suppress;
        if (mapping.Suppress)
        {
            lock (suppressLock) suppressedKeysDown.Add(e.VkCode);
        }

        Console.WriteLine($"[Program] Key '{VirtualKeyMap.NameOf(e.VkCode)}' triggered {mapping.Action.Type}:{mapping.Action.Value} " +
                           $"(suppressed={mapping.Suppress}).");

        // Offload the actual side effect: the hook callback must return quickly or Windows
        // will silently detach a slow LL hook.
        var action = mapping.Action;
        ThreadPool.QueueUserWorkItem(_ => actionExecutor.Execute(action));
    }
    else
    {
        lock (suppressLock)
        {
            if (suppressedKeysDown.Remove(e.VkCode))
            {
                e.Suppress = true;
            }
        }
    }
};

controllerEngine.ButtonPressed += (_, e) =>
{
    var profile = Volatile.Read(ref activeProfile);
    var mapping = profile.ControllerMappings.FirstOrDefault(m => m.Button == e.Button);
    if (mapping is null) return;

    Console.WriteLine($"[Program] Controller {e.ControllerIndex} button '{e.Button}' triggered " +
                       $"{mapping.Action.Type}:{mapping.Action.Value}.");

    var action = mapping.Action;
    ThreadPool.QueueUserWorkItem(_ => actionExecutor.Execute(action));
};

static KeyMapping? FindKeyMapping(Profile profile, int vkCode)
{
    foreach (var mapping in profile.KeyMappings)
    {
        if (VirtualKeyMap.Resolve(mapping.Key) == vkCode) return mapping;
    }
    return null;
}

Console.WriteLine("Input Abstraction & Workflow Automation Engine");
Console.WriteLine("================================================");
Console.WriteLine($"Profiles file: {profilesPath}");

try
{
    keyboardHook.Start();
    Console.WriteLine("[Program] Low-level keyboard hook installed.");

    controllerEngine.Start();
    Console.WriteLine("[Program] Xbox controller polling started.");

    processMonitor.Start();
    Console.WriteLine("[Program] Foreground process monitor started.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Program] Fatal startup failure: {ex.Message}");
    return 1;
}

Console.WriteLine("Running. Press Ctrl+C to exit.");

using var exitRequested = new ManualResetEventSlim(false);
using var cleanupDone = new ManualResetEventSlim(false);

// Console.CancelKeyPress only covers Ctrl+C/Ctrl+Break from this console. SetConsoleCtrlHandler
// also catches the console window being closed or the user logging off/shutting down, which
// matters here because it is the only chance to run UnhookWindowsHookEx before the process dies.
// The handler blocks on cleanupDone (set only after disposal below completes) rather than on
// exitRequested itself, so the OS is genuinely held off until teardown has actually run.
NativeMethods.ConsoleCtrlHandlerRoutine ctrlHandler = ctrlType =>
{
    Console.WriteLine($"[Program] Shutdown signal received (0x{ctrlType:X}). Cleaning up...");
    exitRequested.Set();
    cleanupDone.Wait(TimeSpan.FromSeconds(5));
    return true;
};
NativeMethods.SetConsoleCtrlHandler(ctrlHandler, add: true);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exitRequested.Set();
};

exitRequested.Wait();

try
{
    keyboardHook.Dispose();
    controllerEngine.Dispose();
    processMonitor.Dispose();
}
finally
{
    cleanupDone.Set();
}

Console.WriteLine("[Program] Exiting.");
return 0;
