using System.Globalization;
using System.Text;

namespace BankAccounts
{
    // ===================== Власні винятки =====================

    /// <summary>Недостатньо коштів для зняття (ліміт залежить від типу рахунку).</summary>
    public class InsufficientFundsException : Exception
    {
        public double RequestedAmount { get; }
        public double AvailableAmount { get; }

        public InsufficientFundsException() : base() { }
        public InsufficientFundsException(string? message) : base(message) { }
        public InsufficientFundsException(string? message, Exception? innerException) : base(message, innerException) { }

        public InsufficientFundsException(string? message, double requestedAmount, double availableAmount)
            : base(message)
        {
            RequestedAmount = requestedAmount;
            AvailableAmount = availableAmount;
        }
    }

    /// <summary>Некоректне ім'я власника рахунку.</summary>
    public class InvalidNameException : Exception
    {
        public InvalidNameException() : base() { }
        public InvalidNameException(string? message) : base(message) { }
        public InvalidNameException(string? message, Exception? innerException) : base(message, innerException) { }
    }

    // ===================== Базовий клас =====================

    public abstract class BankAccount : IComparable<BankAccount>
    {
        private string _owner = string.Empty;
        private double _balance;

        public string Owner
        {
            get => _owner;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                _owner = trimmed.Length < 2
                    ? throw new InvalidNameException("Ім'я власника має містити щонайменше 2 символи.")
                    : trimmed;
            }
        }

        // Баланс змінюється лише через Deposit/Withdraw.
        // Від'ємним він може стати тільки в CheckingAccount (овердрафт) — ці межі перевіряє Withdraw.
        public double Balance
        {
            get => _balance;
            protected set => _balance = double.IsFinite(value)
                ? RoundMoney(value)
                : throw new ArgumentOutOfRangeException(nameof(Balance), "Баланс має бути скінченним числом.");
        }

        public abstract string AccountType { get; }

        protected BankAccount(string owner, double balance = 0.0)
        {
            Owner = owner;

            if (balance < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(balance), "Початковий баланс не може бути від'ємним.");
            }

            Balance = balance;
        }

        /// <summary>Базова версія: не можна зняти більше, ніж є на рахунку.</summary>
        public virtual void Withdraw(double amount)
        {
            amount = PrepareAmount(amount);

            if (amount > Balance)
            {
                throw new InsufficientFundsException(
                    $"Недостатньо коштів: запитано {amount:F2}, на рахунку {Balance:F2}.",
                    amount, Balance);
            }

            Balance -= amount;
        }

        /// <summary>Спільна для всіх рахунків, не перевизначається.</summary>
        public void Deposit(double amount)
        {
            amount = PrepareAmount(amount);
            Balance += amount;
        }

        /// <summary>Природний порядок — від меншого балансу до більшого.</summary>
        public int CompareTo(BankAccount? other) => other is null ? 1 : Balance.CompareTo(other.Balance);

        public virtual string GetInfo() => $"{AccountType,-11} | {Owner,-20} | баланс: {Balance,12:F2}";

        public override string ToString() => GetInfo();

        /// <summary>Гроші рахуємо з точністю до копійки, щоб уникнути похибок double (0.3 - 0.1 != 0.2).</summary>
        protected static double RoundMoney(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        protected static double PrepareAmount(double amount)
        {
            double rounded = double.IsFinite(amount) ? RoundMoney(amount) : 0.0;

            if (rounded <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Сума має бути додатною (щонайменше 0.01).");
            }

            return rounded;
        }
    }

    // ===================== Ощадний рахунок =====================

    public class SavingsAccount : BankAccount
    {
        private double _minimumBalance;

        public double MinimumBalance
        {
            get => _minimumBalance;
            set
            {
                if (!double.IsFinite(value) || value < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MinimumBalance), "Мінімальний залишок не може бути від'ємним.");
                }

                double rounded = RoundMoney(value);

                if (Balance < rounded)
                {
                    throw new ArgumentOutOfRangeException(nameof(MinimumBalance),
                        $"Мінімальний залишок ({rounded:F2}) не може перевищувати поточний баланс ({Balance:F2}).");
                }

                _minimumBalance = rounded;
            }
        }

        public override string AccountType => "Ощадний";

        public SavingsAccount(string owner, double balance, double minimumBalance)
            : base(owner, balance)
        {
            MinimumBalance = minimumBalance; // сеттер перевіряє, що balance >= minimumBalance
        }

        /// <summary>Додатково забороняє опускати баланс нижче MinimumBalance.</summary>
        public override void Withdraw(double amount)
        {
            amount = PrepareAmount(amount);

            if (RoundMoney(Balance - amount) < MinimumBalance)
            {
                throw new InsufficientFundsException(
                    $"Не можна опустити баланс нижче мінімального залишку {MinimumBalance:F2}. " +
                    $"Доступно для зняття: {RoundMoney(Balance - MinimumBalance):F2}.",
                    amount, RoundMoney(Balance - MinimumBalance));
            }

            base.Withdraw(amount);
        }

        public override string GetInfo() => base.GetInfo() + $" | мін. залишок: {MinimumBalance:F2}";
    }

    // ===================== Поточний рахунок =====================

    public class CheckingAccount : BankAccount
    {
        private double _overdraftLimit;

        public double OverdraftLimit
        {
            get => _overdraftLimit;
            set
            {
                if (!double.IsFinite(value) || value < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(OverdraftLimit), "Ліміт овердрафту не може бути від'ємним.");
                }

                double rounded = RoundMoney(value);

                if (Balance < -rounded)
                {
                    throw new ArgumentOutOfRangeException(nameof(OverdraftLimit),
                        $"Поточний борг ({Balance:F2}) вже перевищує новий ліміт овердрафту ({rounded:F2}).");
                }

                _overdraftLimit = rounded;
            }
        }

        public override string AccountType => "Поточний";

        public CheckingAccount(string owner, double balance, double overdraftLimit)
            : base(owner, balance)
        {
            OverdraftLimit = overdraftLimit;
        }

        /// <summary>
        /// Дозволяє піти в мінус, але не нижче -OverdraftLimit.
        /// base.Withdraw тут не викликається, бо базова версія забороняє знімати більше за Balance.
        /// </summary>
        public override void Withdraw(double amount)
        {
            amount = PrepareAmount(amount);
            double newBalance = RoundMoney(Balance - amount);

            if (newBalance < -OverdraftLimit)
            {
                throw new InsufficientFundsException(
                    $"Перевищено ліміт овердрафту {OverdraftLimit:F2}. " +
                    $"Доступно для зняття: {RoundMoney(Balance + OverdraftLimit):F2}.",
                    amount, RoundMoney(Balance + OverdraftLimit));
            }

            Balance = newBalance;
        }

        public override string GetInfo() =>
            base.GetInfo() + $" | овердрафт: {OverdraftLimit:F2} | доступно: {RoundMoney(Balance + OverdraftLimit):F2}";
    }

    // ===================== Консольне меню =====================

    static class Program
    {
        public static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            var accounts = new List<BankAccount>
            {
                new SavingsAccount("Іван Петренко", 5000, 1000),
                new CheckingAccount("Олена Коваль", 1200, 500),
                new SavingsAccount("Андрій Мельник", 300, 300),
            };

            bool running = true;

            while (running)
            {
                PrintMenu();
                int choice = ReadInt("Ваш вибір: ", 1, 5);
                Console.WriteLine();

                switch (choice)
                {
                    case 1: running = false; break;
                    case 2: HandleAddAccount(accounts); break;
                    case 3: HandleDeposit(accounts); break;
                    case 4: HandleWithdraw(accounts); break;
                    case 5: HandleListAccounts(accounts); break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("До побачення!");
        }

        private static void PrintMenu()
        {
            Console.WriteLine("========= Банківські рахунки =========");
            Console.WriteLine("1. Вихід");
            Console.WriteLine("2. Додати рахунок");
            Console.WriteLine("3. Поповнити рахунок");
            Console.WriteLine("4. Зняти кошти");
            Console.WriteLine("5. Показати всі рахунки (за спаданням балансу)");
        }

        // ---------- Обробники пунктів меню ----------

        private static void HandleAddAccount(List<BankAccount> accounts)
        {
            Console.WriteLine("Тип рахунку: 1 — ощадний, 2 — поточний");
            int type = ReadInt("Оберіть тип: ", 1, 2);

            Console.Write("Власник: ");
            string owner = Console.ReadLine() ?? string.Empty;
            double balance = ReadDouble("Початковий баланс: ");

            try
            {
                BankAccount account;

                if (type == 1)
                {
                    double minimumBalance = ReadDouble("Мінімальний залишок: ");
                    account = new SavingsAccount(owner, balance, minimumBalance);
                }
                else
                {
                    double overdraftLimit = ReadDouble("Ліміт овердрафту: ");
                    account = new CheckingAccount(owner, balance, overdraftLimit);
                }

                accounts.Add(account);
                Console.WriteLine($"Рахунок створено: {account.GetInfo()}");
            }
            catch (InvalidNameException ex)
            {
                Console.WriteLine($"Помилка імені: {ex.Message}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine($"Некоректне значення: {StripParamName(ex)}");
            }
        }

        private static void HandleDeposit(List<BankAccount> accounts)
        {
            BankAccount? account = ChooseAccount(accounts);
            if (account is null) return;

            double amount = ReadDouble("Сума поповнення: ");

            try
            {
                account.Deposit(amount);
                Console.WriteLine($"Готово. {account.GetInfo()}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine($"Некоректна сума: {StripParamName(ex)}");
            }
        }

        private static void HandleWithdraw(List<BankAccount> accounts)
        {
            BankAccount? account = ChooseAccount(accounts);
            if (account is null) return;

            double amount = ReadDouble("Сума зняття: ");

            try
            {
                account.Withdraw(amount); // поліморфний виклик: версія залежить від реального типу рахунку
                Console.WriteLine($"Готово. {account.GetInfo()}");
            }
            catch (InsufficientFundsException ex)
            {
                Console.WriteLine($"Відмовлено: {ex.Message}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine($"Некоректна сума: {StripParamName(ex)}");
            }
        }

        private static void HandleListAccounts(List<BankAccount> accounts)
        {
            if (accounts.Count == 0)
            {
                Console.WriteLine("Рахунків ще немає.");
                return;
            }

            var sorted = new List<BankAccount>(accounts);
            sorted.Sort((a, b) => b.CompareTo(a)); // IComparable дає зростання, тут — спадання

            Console.WriteLine("Рахунки за спаданням балансу:");
            foreach (BankAccount account in sorted)
            {
                Console.WriteLine(account.GetInfo()); // поліморфний виклик перевизначеного методу
            }
        }

        // ---------- Допоміжні методи ----------

        private static BankAccount? ChooseAccount(List<BankAccount> accounts)
        {
            if (accounts.Count == 0)
            {
                Console.WriteLine("Рахунків ще немає. Спершу додайте рахунок (пункт 2).");
                return null;
            }

            for (int i = 0; i < accounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {accounts[i].GetInfo()}");
            }

            int index = ReadInt("Номер рахунку: ", 1, accounts.Count);
            return accounts[index - 1];
        }

        private static int ReadInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                string? input = Console.ReadLine();

                if (input is null)
                {
                    // Кінець вводу (Ctrl+Z / Ctrl+D) — завершуємо програму, а не зациклюємося.
                    Environment.Exit(0);
                }

                if (int.TryParse(input.Trim(), out int value) && value >= min && value <= max)
                {
                    return value;
                }

                Console.WriteLine($"Введіть ціле число від {min} до {max}.");
            }
        }

        private static double ReadDouble(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? input = Console.ReadLine();

                if (input is null)
                {
                    Environment.Exit(0);
                }

                // Приймаємо і кому, і крапку як десятковий роздільник.
                string normalized = input.Trim().Replace(',', '.');

                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    && double.IsFinite(value))
                {
                    return value;
                }

                Console.WriteLine("Введіть число, наприклад 1500 або 99.50.");
            }
        }

        /// <summary>ArgumentException.Message дописує "(Parameter 'x')" — для користувача це зайве.</summary>
        private static string StripParamName(ArgumentException ex)
        {
            string message = ex.Message;
            int index = message.IndexOf(" (Parameter", StringComparison.Ordinal);
            return index >= 0 ? message[..index] : message;
        }
    }
}