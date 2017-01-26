using static System.Console;

class Program
{
	public static void Main()
	{
		var username = "world";
		WriteLine($"Hello, {username}!");

		WriteLine($"{username.GetType().Assembly.FullName}");
	}
}