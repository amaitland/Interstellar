﻿namespace Interstellar.Core
open System

type BrowserEngineType = | Chromium = 0 | WebKit = 0b1
type BrowserPlatformType = | WindowsWpf = 0 | MacOS = 0b1

type BrowserWindowTitleChangedEventArgs(oldValue, newValue) =
    inherit EventArgs()

    member this.OldValue : string = oldValue
    member this.NewValue : string = newValue

type IBrowserWindow =
    abstract Engine : BrowserEngineType
    abstract Platform : BrowserPlatformType
    abstract Address : string with get, set
    abstract Title : string
    [<CLIEvent>] abstract TitleChanged: IEvent<BrowserWindowTitleChangedEventArgs>