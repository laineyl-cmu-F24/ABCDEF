# Measures / Techniques to Optimize Performance
## Agent
   1. **Concurrency with F# MailboxProcessor (Actor Model):**
      - The use of `MailboxProcessor` in both the **cache agent** and **state agent** ensures safe and efficient state management without explicit locking mechanisms. This actor-based concurrency model helps prevent race conditions by processing messages sequentially.
         - Examples:
            - **Cache Agent** handles market data updates and retrieves quotes asynchronously.
            - **State Agent** manages trading parameters, trade history, and system state with a clear separation of concerns.

   2. **Systematic Shared State Management:**
      - The state agent ensures consistent access to trading parameters and system-wide flags like `IsTradingActive`. This central management reduces overhead and simplifies debugging.
   

## Efficient Data Processing
   1. **Sequence**
      - Sequence transformations (e.g., `Seq.groupBy`, `Seq.filter`) are used to streamline the detection of arbitrage opportunities, reducing redundant computations.
   
   2. **Parallel Data Processing with Asynchronous Workflows:**
      - Some real-time trading function employs **async workflows** and **tasks** to place orders and retrieve their statuses in parallel, reducing latency during trade execution.
         - Examples:
            - `executeTrades` leverage F#'s async workflows to place buy and sell orders concurrently.
            - `getOrderStatus`: Order status retrieval is parallelized, reducing latency in critical operations.
      - Events like `ThresholdExceeded` are handled asynchronously to trigger appropriate system actions without blocking ongoing operations.

## Leveraging F# Features
   1. **Pattern Matching:** Extensively used for conditional logic (e.g., checking spread validity, determining exchange names), simplifying the codebase and reducing errors.
   2. **Pipelining (`|>`):** Makes transformations and filters more readable and efficient by chaining operations without intermediate variables.
   3. **Immutable Data Structures:** Ensures that data remains consistent across concurrent operations.


# Files that Implement Important Functions

1. **Entry Point and Trading Strategy Management**:```ArbitrageGainer/app.fs```
2. **Retrieval of Cross-Traded Currency Pairs**:```ArbitrageGainer/Service/ApplicationService/CurrencyPairRetrieval.fs```
3. **Real-Time Market Data Management**: ```ArbitrageGainer/Service/ApplicationService/MarketData.fs```
4. **Annualized Return Metric Calculation**: ```ArbitrageGainer/Service/ApplicationService/Metric.fs```
5. **Profit and Loss Calculation**: ```ArbitrageGainer/Service/ApplicationService/PnL_Calculation.fs```
6. **Real-Time Trading Algorithms**: 
   1. ```ArbitrageGainer/Service/ApplicationService/TradingAgent.fs```
   2. ```ArbitrageGainer/Service/ApplicationService/OrderManagement.fs```

# Technical Debt
We need to document our code more comprehensively and informatively for a shorter onboarding time when adding new features.