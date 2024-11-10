# APIs
1. ``` POST /api/strategy ```
```
curl -X POST http://localhost:8080/api/strategy \
     -d "NumOfCrypto=5" \
     -d "MinSpreadPrice=10.5" \
     -d "MinTransactionProfit=15.0" \
     -d "MaxTransactionValue=1000.0" \
     -d "MaxTradeValue=500.0" \
     -d "InitialInvestmentAmount=10000.0" \
     -d "Email=test@example.com" \
     -d "PnLThreshold=5.0"

```

2. ``` GET /api/strategy ```
```
curl -X GET http://localhost:8080/api/strategy
```