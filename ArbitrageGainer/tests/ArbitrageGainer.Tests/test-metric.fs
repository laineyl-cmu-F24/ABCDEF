module ArbitrageGainer.Tests.Metric
open NUnit.Framework
open Service.ApplicationService.Metric
open Core.Model.Models

[<Test>]
let ``validateInitialAmount should return Ok for positive amount`` () =
    let result = validateInitialAmount 100M
    match result with
    | Ok amount -> Assert.That(amount, Is.EqualTo(100M))
    | Error _ -> Assert.Fail("Expected Ok but got Error")

[<Test>]
let ``validateInitialAmount should return Error for zero amount`` () =
    let result = validateInitialAmount 0M
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error (ValidationError NegativeInitialInvestment) -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``validateInitialAmount should return Error for negative amount`` () =
    let result = validateInitialAmount -50M
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error (ValidationError NegativeInitialInvestment) -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``validateTimeInterval should return Ok for valid time range`` () =
    let startTime = 1000000L
    let endTime = 2000000L
    let result = validateTimeInterval startTime endTime
    match result with
    | Ok (starting, ending) -> 
        Assert.That(starting, Is.EqualTo(startTime))
        Assert.That(ending, Is.EqualTo(endTime))
    | Error _ -> Assert.Fail("Expected Ok but got Error")

[<Test>]
let ``validateTimeInterval should return Error for invalid time range`` () =
    let startTime = 2000000L
    let endTime = 1000000L
    let result = validateTimeInterval startTime endTime
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error (ValidationError InvalidTimeRange) -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")


[<Test>]
let ``AnnualizedMetric should return Ok with correct metric for valid input`` () =
    let initialAmount = 1000M
    let startingTime = Some 1729728000000L
    let pnlValue = 1500M // Mock PnL value
    match AnnualizedMetric initialAmount startingTime pnlValue with
    | Ok metric -> Assert.Pass($"Expected Ok with result: {metric}")
    | Error _ -> Assert.Fail("Expected Ok but got Error")

[<Test>]
let ``AnnualizedMetric should return Error for negative initial amount`` () =
    let initialAmount = -100M
    let startingTime = Some 1729728000000L
    let pnlValue = 1500M // Mock PnL value
    match AnnualizedMetric initialAmount startingTime pnlValue with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error (ValidationError NegativeInitialInvestment) -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``AnnualizedMetric should return Error for zero initial amount`` () =
    let initialAmount = 0M
    let startingTime = Some 1729728000000L
    let pnlValue = 1500M // Mock PnL value
    match AnnualizedMetric initialAmount startingTime pnlValue with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error (ValidationError NegativeInitialInvestment) -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")