namespace Logging
    open System.IO
    open System

    module Logger = 
        // Define a function to create a logger function
        let createLogger =
            // Define the log file path
            let filePath = "/Service/ApplicationService/log.txt"

            // Ensure the directory exists
            let ensureLogFile () =
                try
                    // Try creating the directory and file; ignore exceptions if they already exist
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)) |> ignore
                    use _ = File.Open(filePath, FileMode.OpenOrCreate) // Open or create the file
                    ()
                with
                | _ -> () // Swallow any exception silently (or handle as needed)


            // Logger function
            let logMessage (invokingWorkflowName: string) =
                ensureLogFile()
                // Prepare log entry with current date, time, and invoking workflow name
                let logEntry = sprintf "%s - %s called" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff")) invokingWorkflowName
                printfn "%A" logEntry
                // Append log entry to the file
                File.AppendAllText(filePath, logEntry + "\n")
            
            // Return the logging function
            logMessage