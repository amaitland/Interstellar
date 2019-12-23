﻿namespace Examples.SharedCode
open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.Versioning
open System.Threading
open Interstellar

module AppletIds =
    let [<Literal>] Calculator = "calculator"
    let [<Literal>] InjectedContent = "injectedContent"
    let [<Literal>] InterstellarDetector = "detector"
    let [<Literal>] InterWindowCommunication = "interwindow"

module SimpleBrowserApp =
    let runtimeFramework = Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName
    let detectorPageUrl = new Uri("https://gist.githack.com/jwosty/239408aaffd106a26dc2161f86caa641/raw/5af54d0f4c51634040ea3859ca86032694afc934/interstellardetector.html")

    let showCalculatorWindow mainCtx (createWindow: BrowserWindowCreator) = async {
        let page = """
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="utf-8"/>
                    <script>
                        function isNumeric(x) {
                            return !isNaN(x)
                        }
                        function recalculate() {
                            var input1 = Number(document.getElementById("input1").value)
                            var input2 = Number(document.getElementById("input2").value)
                            var operand = document.getElementById("operand").value
                            var fOperand = (x,y) => x + y
                            if (operand === "sub") {
                                fOperand = (x,y) => x - y
                            } else if (operand === "mul") {
                                fOperand = (x,y) => x * y
                            } else if (operand === "div") {
                                fOperand = (x,y) => x / y
                            }
                            document.getElementById("result").textContent = fOperand(input1, input2)
                        }
                    </script>
                </head>
                <body>
                    <span>
                        <input type="number" id="input1" oninput="recalculate()" />
                        <select id="operand" onchange="recalculate()">
                            <option value="add">+</option>
                            <option value="sub">-</option>
                            <option value="mul">×</option>
                            <option value="div">÷</option>
                        </select>
                        <input type="number" id="input2" oninput="recalculate()" /t>
                        <span>=</span>
                        <span id="result" />
                    </span>
                </body>
            </html>"""
        let window = createWindow { defaultBrowserWindowConfig with html = Some page }
        do! window.Show ()
        return window
    }

    let fileWriteTextAsync path (string: string) = async {
        use fileOut = File.CreateText path
        do! Async.AwaitTask (fileOut.WriteAsync string)
    }

    let showInjectedContentWindow mainCtx (createWindow: BrowserWindowCreator) = async {
        // to make this one a little more interesting, lets show the content from a temp file instead of the data-style URI like the other examples,
        // so it can feel a bit more like a "real page"
        let filePath = Path.Combine (Path.GetTempPath(), Path.ChangeExtension (Guid.NewGuid().ToString(), ".html"))
        let fileUri = Uri(filePath)
        let content = sprintf """
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <p>This file can be found at <a href="%s">%s</a>
                    <p>UI host: <span id="platform">Unknown</span></p>
                    <p>Browser engine: <span id="browserEngine">Unknown</span></p>
                    <p>Runtime framework: <span id="runtimeFramework">Unknown</span></p>
                </body>
            </html>""" fileUri.AbsoluteUri fileUri.AbsoluteUri
        Trace.WriteLine (sprintf "temp html file path: %s" filePath)
        do! fileWriteTextAsync filePath content
        let window = createWindow defaultBrowserWindowConfig
        do! window.Show ()
        let! handleToAwaitJSReady = window.Browser.LoadAsync fileUri
        do! handleToAwaitJSReady
        window.Browser.ExecuteJavascript
            (sprintf "document.getElementById('runtimeFramework').textContent='%s';document.getElementById('platform').textContent='%A';document.getElementById('browserEngine').textContent='%A'"
                runtimeFramework window.Platform window.Browser.Engine)
        return window
    }

    let showDetectorWindow mainCtx (createWindow: BrowserWindowCreator) = async {
        let window = createWindow { defaultBrowserWindowConfig with address = Some detectorPageUrl }
        do! window.Show ()
        return window
    }

    let runCrossCommunicatingWindows (mainCtx: SynchronizationContext) (createWindow: BrowserWindowCreator) = async {
        let inputPage = """
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="utf-8"/>
                    <title>Input window</title>
                    <script>
                        function sendToOtherWindow() {
                            interstellarBridge.postMessage(document.getElementById("theInput").value)
                        }
                    </script>
                </head>
                <body>
                    <input id="theInput" placeholder="Enter some text here" oninput="sendToOtherWindow()" />
                </body>
            </html>"""
        let outputPage = """
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="utf-8"/>
                    <title>Output window</title>
                    <script>
                        function updateOutput(newText) {
                            document.getElementById("theOutput").textContent = newText
                        }
                    </script>
                </head>
                <body>
                    <p>Reversed input text: <span id="theOutput"/></p>
                </body>
            </html>"""
        let inputWindow = createWindow { defaultBrowserWindowConfig with html = Some inputPage }
        let outputWindow = createWindow { defaultBrowserWindowConfig with html = Some outputPage }

        inputWindow.Browser.JavascriptMessageRecieved.Add (fun msg ->
            mainCtx.Post (SendOrPostCallback(fun _ ->
                if outputWindow.IsShowing then
                    outputWindow.Browser.ExecuteJavascript (sprintf "updateOutput('%s')" (String (Array.rev (msg.ToCharArray ()))))
            ), null)   
        )

        do! inputWindow.Show ()
        do! outputWindow.Show ()
        // await both closed
        do! Async.Ignore <| Async.Parallel [Async.AwaitEvent inputWindow.Closed; Async.AwaitEvent outputWindow.Closed]
    }

    let appletSelectorWindow mainCtx (createWindow: BrowserWindowCreator) = async {
        let page = sprintf """
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset="utf-8"/>
                    <script>
                        function ex(which) {
                            interstellarBridge.postMessage(which)
                        }
                    </script>
                </head>
                <body>
                    <button onclick="ex('%s')">Calculator</button>
                    <br>
                    <button onclick="ex('%s')">App-injected content</button>
                    <br>
                    <button onclick="ex('%s')">Interstellar detector</button> - %s
                    <br>
                    <button onclick="ex('%s')">Inter window communication</button>
                </body>
            </html>""" AppletIds.Calculator AppletIds.InjectedContent AppletIds.InterstellarDetector detectorPageUrl.AbsoluteUri AppletIds.InterWindowCommunication
        let selectorWindow = createWindow { defaultBrowserWindowConfig with html = Some page }
        do! selectorWindow.Show ()
        selectorWindow.Browser.JavascriptMessageRecieved.Add (fun msg ->
            Async.Start <| async {
                do! Async.SwitchToContext mainCtx
                match msg with
                | AppletIds.Calculator -> do! Async.Ignore (showCalculatorWindow mainCtx createWindow)
                | AppletIds.InjectedContent -> do! Async.Ignore (showInjectedContentWindow mainCtx createWindow)
                | AppletIds.InterstellarDetector -> do! Async.Ignore (showDetectorWindow mainCtx createWindow)
                | AppletIds.InterWindowCommunication -> do! runCrossCommunicatingWindows mainCtx createWindow
                | msg -> Trace.WriteLine (sprintf "Bad message: %s" msg)
            })
        return selectorWindow
    }

    let app = BrowserApp.create (fun mainCtx createWindow -> async {
        let! mainWindow = appletSelectorWindow mainCtx createWindow
        do! Async.AwaitEvent mainWindow.Closed
    })