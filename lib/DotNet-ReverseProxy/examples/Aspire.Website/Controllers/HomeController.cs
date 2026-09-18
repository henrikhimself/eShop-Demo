using Microsoft.AspNetCore.Mvc;

namespace Hj.Examples.Aspire.Website.Controllers;

public class HomeController : Controller
{
  public IActionResult Index() => View();
}
