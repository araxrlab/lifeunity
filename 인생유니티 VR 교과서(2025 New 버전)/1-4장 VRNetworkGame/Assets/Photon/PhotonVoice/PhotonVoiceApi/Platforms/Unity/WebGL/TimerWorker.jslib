var LibraryTimerWorker = {
    $Photon_JS_TimerWorker_handles: new Map(),
    $Photon_JS_TimerWorker_handleCnt: 0,
    Photon_JS_TimerWorker_Start: function(callback, interval) {
        console.log("[Photon] Photon_JS_TimerWorker_Start", interval);
        
        const workerFoo = // minification-friendly "to string conversion", comment out `s for dev
        `
        function() {
            let timer;
            const job = (ev) => {
                const interval = ev.data;
                timer = setInterval(() => postMessage(0), interval);
            }
            onmessage = job;
        }
        `

        let ws = workerFoo.toString();
        ws = ws.substring(ws.indexOf("{") + 1, ws.lastIndexOf("}"));
        const blob = new Blob([ws], {
            type: "text/javascript"
        });

        const workerURL = window.URL.createObjectURL(blob);
        const worker = new Worker(workerURL);
        const handle = Photon_JS_TimerWorker_handleCnt++;
        worker.onmessage = () => { {{{ makeDynCall('vi', 'callback') }}}(handle); }
        worker.postMessage(interval);
        Photon_JS_TimerWorker_handles[handle] = worker;
        return handle;
    },
    
    Photon_JS_TimerWorker_Stop: function(handle) {
        console.log("[Photon] Photon_JS_TimerWorker_Stop", handle);
        Photon_JS_TimerWorker_handles[handle].terminate();
        Photon_JS_TimerWorker_handles.delete(handle);
        return 0
    }
};

autoAddDeps(LibraryTimerWorker, '$Photon_JS_TimerWorker_handles');
autoAddDeps(LibraryTimerWorker, '$Photon_JS_TimerWorker_handleCnt');
mergeInto(LibraryManager.library, LibraryTimerWorker);
