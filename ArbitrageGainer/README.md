# Milestone II Doc

1. **Trading Strategy Management**:```ArbitrageGainer/app.fs```
2. **Retrieval of Cross-Traded Currency Pairs**:```ArbitrageGainer/Service/ApplicationService/CurrencyPairRetrieval.fs```
3. **Real-Time Market Data Management**: ```ArbitrageGainer/Service/ApplicationService/MarketData.fs```
4. **Annualized Return Metric Calculation**: ```ArbitrageGainer/Service/ApplicationService/Metric.fs```
5. **Profit and Loss Calculation**: ```ArbitrageGainer/Service/ApplicationService/PnL_Calculation.fs```


# Run Code in Docker
1. Build with **docker build -f Dockerfile --platform linux/amd64 -t pascalyang/18656_01 .**
2. Run with **docker run -p 8080:8080 pascalyang/18656_01**