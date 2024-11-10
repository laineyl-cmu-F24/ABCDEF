module ArbitrageGainer.Tests.Metric
open NUnit.Framework
open Metric


[<Test>]
let ``validateInitialAmount should return Ok for positive amount`` () =
    let result = validateInitialAmount 100M
    match result with
    | Ok amount -> Assert.That(amount, Is.EqualTo(100M))
    | Error _ -> Assert.Fail("Expected Ok but got Error")

[<Test>]
let ``validateInitialAmount should return Error for negative amount`` () =
    let result = validateInitialAmount -50M
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error NegativeInitialInvestment -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")