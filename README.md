# Milestone III Update

1. **Entry Point and Trading Strategy Management**:```ArbitrageGainer/app.fs```
2. **Retrieval of Cross-Traded Currency Pairs**:```ArbitrageGainer/Service/ApplicationService/CurrencyPairRetrieval.fs```
3. **Real-Time Market Data Management**: ```ArbitrageGainer/Service/ApplicationService/MarketData.fs```
4. **Annualized Return Metric Calculation**: ```ArbitrageGainer/Service/ApplicationService/Metric.fs```
5. **Profit and Loss Calculation**: ```ArbitrageGainer/Service/ApplicationService/PnL_Calculation.fs```
6. **Real-Time Trading Algorithms**: 
   1. ```ArbitrageGainer/Service/ApplicationService/TradingAgent.fs```
   2. ```ArbitrageGainer/Service/ApplicationService/OrderManagement.fs```

# Technical Debt
1. We compromised partial onion architecture restructuring to prioritize code compilation.
2. We need to perform style check on current code.
3. We need to enhance edge case failure prevention and standardize error handling.
