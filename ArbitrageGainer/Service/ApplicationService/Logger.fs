namespace Logging
    open System.IO
    open System

    module Logger = 
        // Define a function to create a logger function
        let createLogger =
            let filePath = Path.Combine(__SOURCE_DIRECTORY__, "log.txt")

            // Logger function
            let logMessage (invokingWorkflowName: string) =
                // Prepare log entry with current date, time, and invoking workflow name
                let logEntry = sprintf "%s - %s called" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff")) invokingWorkflowName
                printfn "%A" logEntry
                // Append log entry to the file
                File.AppendAllText(filePath, logEntry + "\n")
            
            // Return the logging function
            logMessage