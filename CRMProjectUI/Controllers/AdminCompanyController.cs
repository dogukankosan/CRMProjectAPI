using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Mvc;

[Route("AdminSirket")]
public class AdminCompanyController : Controller
{
    private readonly CompanyApiService _companyApiService;
    public AdminCompanyController(CompanyApiService companyApiService)
    {
        _companyApiService = companyApiService;
    }
    [HttpGet("Liste")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var companies = await _companyApiService.GetCompanyAsync();
            return View(companies);
        }
        catch
        {
            TempData["Error"] = "Firma bilgileri alınamadı";
            return View(new List<CompanyDto>());
        }
    }
}