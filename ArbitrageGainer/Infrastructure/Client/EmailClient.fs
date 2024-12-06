module ArbitrageGainer.Infrastructure.Client.EmailClient

open System.Net
open System.Net.Mail
let smtpServer = "smtp.gmail.com"
let smtpPort = 587
let fromEmail = "pascal011206@gmail.com"
let fromPassword = "dobloolclrziodqw"

let sendEmail (toEmail: string) (subject:string) (body:string) =
    // Set up the SMTP client
    use smtpClient = new SmtpClient(smtpServer, smtpPort)
    smtpClient.Credentials <- NetworkCredential(fromEmail, fromPassword) :> ICredentialsByHost
    smtpClient.EnableSsl <- true

    // Create the email message
    let mailMessage = new MailMessage()
    mailMessage.From <- MailAddress(fromEmail)
    mailMessage.To.Add(toEmail)
    mailMessage.Subject <- subject
    mailMessage.Body <- body

    // Send the email
    smtpClient.Send(mailMessage)
    printfn "Email sent successfully to %s" toEmail

// Example usage
//
// let toEmail = "pascal@wustl.edu"
// let subject = "New Email!"
// let body = "This is a test email sent from an F# program."
//
// sendEmail toEmail subject body
