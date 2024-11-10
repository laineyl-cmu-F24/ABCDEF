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
let ``validateInitialAmount should return Error for zero amount`` () =
    let result = validateInitialAmount 0M
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error NegativeInitialInvestment -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``validateInitialAmount should return Error for negative amount`` () =
    let result = validateInitialAmount -50M
    match result with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error NegativeInitialInvestment -> Assert.Pass()
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
    | Error InvalidTimeRange -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``calculateAnnualizedMetric should calculate correct metric for 1-year duration and 10% growth`` () =
    let startTime = 1609459200000L // January 1, 2021
    let endTime = 1640995200000L   // January 1, 2022
    let initialInvestment = 1000M
    let pl = 100M                  // Profit/loss
    let result = calculateAnnualizedMetric startTime endTime pl initialInvestment
    Assert.That(result, Is.EqualTo(0.1M).Within(0.01M)) // Adjust expected result based on formula

[<Test>]
let ``calculateAnnualizedMetric should handle zero profit/loss correctly`` () =
    let startTime = 1609459200000L // January 1, 2021
    let endTime = 1640995200000L   // January 1, 2022
    let initialInvestment = 1000M
    let pl = 0M                    // No growth
    let result = calculateAnnualizedMetric startTime endTime pl initialInvestment
    Assert.That(result, Is.EqualTo(0M))

[<Test>]
let ``AnnualizedMetric should return Ok with correct metric for valid input`` () =
    let initialAmount = 1000M
    match AnnualizedMetric initialAmount with
    | Ok metric -> Assert.Pass($"Expected Ok with result: {metric}")
    | Error _ -> Assert.Fail("Expected Ok but got Error")

[<Test>]
let ``AnnualizedMetric should return Error for negative initial amount`` () =
    let initialAmount = -100M
    match AnnualizedMetric initialAmount with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error NegativeInitialInvestment -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")

[<Test>]
let ``AnnualizedMetric should return Error for zero initial amount`` () =
    let initialAmount = 0M
    match AnnualizedMetric initialAmount with
    | Ok _ -> Assert.Fail("Expected Error but got Ok")
    | Error NegativeInitialInvestment -> Assert.Pass()
    | Error _ -> Assert.Fail("Unexpected error type")