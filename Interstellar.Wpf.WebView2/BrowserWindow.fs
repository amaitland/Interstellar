﻿namespace Interstellar.Wpf.WebView2
open System
open System.Threading
open System.Windows
open Microsoft.Web.WebView2.Core
open Microsoft.Web.WebView2.Wpf
open Interstellar
open Interstellar.Core

type BrowserWindow(config: BrowserWindowConfig<Window>) as this =
    inherit Window()

    let mainCtx = SynchronizationContext.Current
    //let cefBrowser = new CefSharp.Wpf.ChromiumWebBrowser()
    let msBrowser = new Microsoft.Web.WebView2.Wpf.WebView2()
    //let browser =
    //    new Interstellar.Wpf.Browser<_>(
    //        cefBrowser,
    //        { getPageTitle = fun () -> cefBrowser.Title
    //          titleChanged = cefBrowser.TitleChanged |> Event.map (fun x -> x.NewValue :?> string)
    //          isBrowserInitializedChanged = cefBrowser.IsBrowserInitializedChanged |> Event.map ignore},
    //        config)
    let browser = Unchecked.defaultof<IBrowser>
    let owningThreadId = Thread.CurrentThread.ManagedThreadId

    let mutable alreadyShown = false
    let shown = new Event<unit>()

    // (primary) constructor
    do
        this.Content <- msBrowser

    interface IDisposable with
        member this.Dispose () =
            Async.StartImmediate <| async {
                do! Async.SwitchToContext mainCtx
                this.Close ()
            }

    interface IBrowserWindow<Window> with
        member this.Browser = browser
        member this.Close () = (this :> Window).Close ()
        [<CLIEvent>] member val Closed = (this :> Window).Closed |> Event.map ignore
        member this.IsShowing =
            seq { for w in Application.Current.Windows -> w }
            |> Seq.contains (this :> Window)
        member this.NativeWindow = this :> Window
        member this.Platform = BrowserWindowPlatform.Wpf
        member this.Show () =
            if owningThreadId <> Thread.CurrentThread.ManagedThreadId then
                raise (new InvalidOperationException("Show() called from a thread other than the thread on which the BrowserWindow was constructed."))
            (this :> Window).Show ()
            async { () }
            //async {
            //    if not msBrowser.IsBrowserInitialized then
            //        let! _ = Async.AwaitEvent msBrowser.IsBrowserInitializedChanged
            //        ()
            //}
        member this.Size
            with get () = base.Width, base.Height
            and set (width, height) =
                base.Width <- width
                base.Height <- height
        [<CLIEvent>] member val Shown = shown.Publish
        member this.Title
            with get () = (this :> Window).Title
            and set title = (this :> Window).Title <- title

    override this.OnContentRendered e =
        base.OnContentRendered e
        if not alreadyShown then
            alreadyShown <- true
            shown.Trigger ()