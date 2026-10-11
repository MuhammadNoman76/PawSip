namespace PawSip.Core;

/// <summary>What the cat says.</summary>
public static class Messages
{
    private static readonly Random Rng = new();

    private static readonly string[] Reminders =
    {
        "Meow! Time to drink some water!",
        "Psst… your water bottle misses you!",
        "Hydration break! Sip sip sip~",
        "Paws up! Let's drink a glass together.",
        "A hydrated human is a happy human!",
        "Water o'clock! The cat has spoken.",
        "Mrrp? Is that a glass of water I see?",
        "Stay fresh like a cat nap — drink some water!",
    };

    private static readonly string[] Behind =
    {
        "We're a little behind today… one glass now?",
        "Meow! Let's catch up — a glass of water, please!",
    };

    private static readonly string[] Morning =
    {
        "Good morning! Start your day with a glass of water.",
        "Rise and shine! First sip of the day?",
    };

    private static readonly string[] Thanks =
    {
        "Purr-fect! Good job!",
        "Yay! Your body says thank you!",
        "Nya~ So proud of you!",
        "Sip sip hooray!",
    };

    private static readonly string[] Pets =
    {
        "Purrrr~",
        "Mrrrow! Thank you!",
        "Hehe, that tickles!",
        "*happy kneading*",
    };

    public static string Reminder(bool firstOfDay, int pace) =>
        firstOfDay ? Pick(Morning) : pace < 0 && Rng.Next(2) == 0 ? Pick(Behind) : Pick(Reminders);

    public static string Thank(bool goalReached) =>
        goalReached ? "Goal reached! You're paw-some!" : Pick(Thanks);

    public static string Pet() => Pick(Pets);

    public static string Greeting()
    {
        var h = DateTime.Now.Hour;
        return h < 5 ? "Hello, night owl" : h < 12 ? "Good morning" : h < 17 ? "Good afternoon" : "Good evening";
    }

    private static string Pick(string[] items) => items[Rng.Next(items.Length)];
}
