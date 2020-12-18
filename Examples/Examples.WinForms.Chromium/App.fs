namespace Example.Windows.Chromium.Wpf
open System
open System.Diagnostics
open System.Reflection
open System.Threading
open System.Windows
open System.Windows.Forms
open Interstellar
open Examples.SharedCode
open Interstellar.Chromium.WinForms
open System.Runtime.Versioning

module Main =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let runApp () =
        Application.EnableVisualStyles ()
        Application.SetCompatibleTextRenderingDefault true
        let onMainWindowCreated (w: IBrowserWindow<Form>) =
            let nativeWindow = w.NativeWindow
            // This is where you could call some WinForms-specific APIs on this window
            ()
        BrowserApp.run (SimpleBrowserApp.app onMainWindowCreated)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let shutdownCefSharp() =
        Interstellar.Chromium.Platform.Shutdown ()
        
    
    [<EntryPoint; STAThread>]
    let main argv =
        Thread.CurrentThread.Name <- "Main"
        Trace.WriteLine (sprintf "Starting app. Main thread id: %A" Thread.CurrentThread.ManagedThreadId)
        Interstellar.Chromium.Platform.Initialize ()
        runApp ()
        shutdownCefSharp()
        0