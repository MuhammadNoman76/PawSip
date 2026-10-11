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

    private static readonly string[] WelcomeBack =
    {
        "Welcome back! Let's start with a glass of water.",
        "You're back! I saved you a sip — drink some water?",
        "Mrrp! Missed you. Time for a glass of water!",
    };

    public static string Reminder(ReminderKind kind, int pace) => kind switch
    {
        ReminderKind.FirstOfDay => Pick(Morning),
        ReminderKind.WelcomeBack => Pick(WelcomeBack),
        _ => pace < 0 && Rng.Next(2) == 0 ? Pick(Behind) : Pick(Reminders),
    };

    private static readonly string[] Nudges =
    {
        "Meow? You still haven't had that glass…",
        "I'm back! Just one glass, please?",
        "Psst… the water is still waiting for you!",
        "Mrrow! Don't make me ask again~ Sip sip!",
    };

    private static readonly string[] Pleading =
    {
        "Pretty please? I'll keep coming back until you drink!",
        "Okay, this cat is not giving up. Water time!",
        "I'm very persistent… one glass and I'll nap!",
    };

    /// <summary>For a reminder that comes back because the last one ended without a drink.</summary>
    public static string Nudge(int missedInARow) => missedInARow >= 3 ? Pick(Pleading) : Pick(Nudges);

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
