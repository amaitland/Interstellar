﻿namespace Interstellar.MacOS.WebKit.Internal
open System
open AppKit
open CoreGraphics
open Foundation
open Interstellar
open Interstellar.MacOS.WebKit
open WebKit

type NiblessViewController(view: NSView) =
    inherit NSViewController()

    override this.LoadView () =
        base.View <- view

type BrowserWindow(config: BrowserWindowConfig) as this =
    inherit NSWindowController("BrowserWindow")

    let wkBrowser = new WKWebView(CGRect.Empty, new WKWebViewConfiguration())
    let browser = new Browser(config, wkBrowser)

    let closed = new Event<_>()
    let shown = new Event<_>()

    do
        let wkBrowserController = {
            new NiblessViewController(wkBrowser) with
                override this.ViewDidAppear () =
                    base.ViewDidAppear ()
                    shown.Trigger ()
        }
        this.Window <-
            new NSWindow(new CGRect (0., 0., 1000., 500.),
                         NSWindowStyle.Titled ||| NSWindowStyle.Closable ||| NSWindowStyle.Miniaturizable ||| NSWindowStyle.Resizable,
                         NSBackingStore.Buffered, false, Title = "My Window")
        this.Window.WillClose.Add (fun x -> closed.Trigger ())
        this.Window.ContentView <- wkBrowser
        this.Window.ContentViewController <- wkBrowserController
        //wkBrowser.LoadRequest (new NSUrlRequest(new NSUrl("https://google.com/"))) |> ignore
        this.Window.Center ()
        this.Window.AwakeFromNib ()


    member this.WKBrowserView = wkBrowser
    member this.WKBrowser = browser

    override this.LoadWindow () =
        base.LoadWindow ()

    //override this.LoadWindow () =
        ////let window = new NSWindow()
        ////window.ContentView <- wkBrowser
        ////window.IsVisible <- true

        ////wkBrowser.AddConstraints [|
        ////    NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, wkBrowser, NSLayoutAttribute.Leading, nfloat 1., nfloat 0.)
        ////    NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, wkBrowser, NSLayoutAttribute.Trailing, nfloat 1., nfloat 0.)
        ////    NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, wkBrowser, NSLayoutAttribute.Top, nfloat 1., nfloat 0.)
        ////    NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, wkBrowser, NSLayoutAttribute.Bottom, nfloat 1., nfloat 0.)
        ////|]
        //base.LoadWindow ()
        //()

    interface IBrowserWindow with
        member this.Browser = upcast browser
        member this.Close () = (this :> NSWindowController).Close ()
        member this.Platform = BrowserWindowPlatform.MacOS
        [<CLIEvent>]
        member this.Closed = closed.Publish
        member this.Show () = async {
            (this :> NSWindowController).ShowWindow this
        }
        [<CLIEvent>]
        member this.Shown = shown.Publish
        member this.Size
            with get () =
                let size = this.Window.Frame.Size
                float size.Width, float size.Height
            and set (width, height) =
                let oldFrame = this.Window.Frame
                // Cocoa uses a bottom-left origin, so we have to move the bottom-left corner in order to keep the top-right
                // corner in place
                let rect = new CGRect(float oldFrame.X, float oldFrame.Y - (height - float oldFrame.Height), width, height)
                this.Window.SetFrame (rect, true, true)
        member this.Title
            with get () = base.Window.Title
            and set x = base.Window.Title <- x