namespace Rxnxt.Logging.Interfaces;

public interface IAppLoggerFactory
{
    IAppLogger CreateLogger(string categoryName);
    IAppLogger CreateLogger<TCategoryName>();
}
