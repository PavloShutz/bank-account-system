# Bank Accounting System

A C# console application for managing savings and checking accounts through a text menu. **The interface, error messages and code comments are in Ukrainian.**

With it you can:

- create savings and checking accounts
- deposit and withdraw money
- list all accounts, sorted by balance from highest to lowest

| Account type | Class | Withdrawal rule |
|---|---|---|
| Savings (`Ощадний`) | `SavingsAccount` | The balance can't drop below the account's minimum balance. |
| Checking (`Поточний`) | `CheckingAccount` | The balance can go negative, down to the overdraft limit. |

Both types inherit from the abstract `BankAccount` class. It checks input, rounds money to two decimal places (midpoints away from zero) and compares accounts by balance through `IComparable<BankAccount>`. Amounts are stored as `double`.

Data is kept in memory only, so changes are lost when you exit. Each launch starts with three sample accounts:

| Owner | Type | Balance | Minimum balance / overdraft limit |
|---|---|---|---|
| Іван Петренко | Savings | 5000 | Minimum balance: 1000 |
| Олена Коваль | Checking | 1200 | Overdraft limit: 500 |
| Андрій Мельник | Savings | 300 | Minimum balance: 300 |

## Installation

You need [Git](https://git-scm.com/) and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The project has no NuGet package dependencies.

```bash
git clone https://github.com/PavloShutz/bank-account-system.git
cd bank-account-system
dotnet build BankAccounts.slnx
```

## Run Locally

From the repository root:

```bash
dotnet run --project BankAccounts
```

The app sets console output to UTF-8. If Ukrainian text still looks wrong, use a terminal and font that support Cyrillic.

## Tech Stack

- **Language:** C#, with nullable reference types and implicit usings enabled
- **Platform:** .NET 10 (`net10.0`)
- **Application type:** console application
- **Libraries:** .NET base class library only
- **Solution format:** `.slnx`

```text
bank-account-system/
├── BankAccounts/
│   ├── BankAccounts.csproj
│   └── Program.cs          # exceptions, account classes and console menu
├── BankAccounts.slnx
└── README.md
```

## Usage/Examples

### Menu

```text
========= Банківські рахунки =========
1. Вихід
2. Додати рахунок
3. Поповнити рахунок
4. Зняти кошти
5. Показати всі рахунки (за спаданням балансу)
Ваш вибір:
```

| Option | Action |
|---|---|
| 1 | Exit |
| 2 | Add an account |
| 3 | Deposit money |
| 4 | Withdraw money |
| 5 | Show all accounts, highest balance first |

When entering numbers, you can use a dot or a comma as the decimal separator (`99.50` or `99,50`). Don't use thousands separators. Deposits and withdrawals must be at least `0.01` after rounding.

### Example: overdraft limit

The examples below are based on the source code and were not captured from a running session. The decimal separator in displayed amounts may differ with your system's regional settings.

Choose option `4`, then account `2` (balance 1200, overdraft limit 500). Entering `1800` is refused:

```text
Сума зняття: 1800
Відмовлено: Перевищено ліміт овердрафту 500.00. Доступно для зняття: 1700.00.
```

The balance stays the same. If you repeat the steps and enter `1700`, the withdrawal succeeds and the balance becomes `-500.00`.

### Example: minimum balance

On a fresh launch, choose option `4`, then account `1` (balance 5000, minimum balance 1000). Entering `4500` is refused:

```text
Сума зняття: 4500
Відмовлено: Не можна опустити баланс нижче мінімального залишку 1000.00. Доступно для зняття: 4000.00.
```

### Using the classes in code

The classes are in the `BankAccounts` namespace:

```csharp
using BankAccounts;

var savings = new SavingsAccount("Марія Шевченко", balance: 2000, minimumBalance: 500);
var checking = new CheckingAccount("Тарас Бондар", balance: 100, overdraftLimit: 300);

savings.Deposit(250);   // balance: 2250
checking.Withdraw(350); // balance: -250

try
{
    savings.Withdraw(2000); // would leave 250, below the minimum of 500
}
catch (InsufficientFundsException ex)
{
    Console.WriteLine($"{ex.Message} (available: {ex.AvailableAmount})");
}

bool lower = checking < savings; // compares balances
```

| Exception | Thrown when |
|---|---|
| `InsufficientFundsException` | A withdrawal breaks the account's limit. |
| `InvalidNameException` | The owner's name has fewer than 2 characters after trimming. |
| `ArgumentOutOfRangeException` | An amount, initial balance or account limit is invalid. |

### Known limitation

Equality isn't implemented yet. `Equals` between two different non-null objects throws `NotImplementedException`, and so do `==` and `!=` between two different accounts. `GetHashCode` always throws it. Avoid equality-based operations, such as using accounts in a `HashSet` or as dictionary keys. The `<`, `<=`, `>` and `>=` operators compare balances.

## Authors

- Pavlo Shutz (Pavlo Shchutskyi): [github.com/PavloShutz](https://github.com/PavloShutz)
