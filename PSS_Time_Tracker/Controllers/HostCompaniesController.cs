using Microsoft.AspNetCore.Mvc;
using PSS_Time_Tracker.Data;
using TimeSheetRecorder.Models;
using System.Threading.Tasks;
using PSS_Time_Tracker.Models;
using Microsoft.EntityFrameworkCore;

namespace PSS_Time_Tracker.Controllers
{
    public class HostCompaniesController : Controller
    {
        private readonly timeSheetRecorderContext _context;

        public HostCompaniesController(timeSheetRecorderContext context)
        {
            _context = context;
        }

        // GET: HostCompanies/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: HostCompanies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] HostCompany hostCompany)
        {
            if (ModelState.IsValid)
            {
                _context.HostCompanies.Add(hostCompany);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(hostCompany);
        }

        // GET: HostCompanies/Index
        public async Task<IActionResult> Index(int page = 1, string searchTerm = null)
        {
            int pageSize = 10;
            var query = _context.HostCompanies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(c => c.Name.Contains(searchTerm));
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)System.Math.Ceiling(totalRecords / (double)pageSize);

            var companies = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;
            ViewBag.SearchTerm = searchTerm;

            return View(companies);
        }

        // GET: HostCompanies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var company = await _context.HostCompanies.FindAsync(id);
            if (company == null) return NotFound();
            return View(company);
        }

        // POST: HostCompanies/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] HostCompany hostCompany)
        {
            if (id != hostCompany.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(hostCompany);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(hostCompany);
        }

        // POST: HostCompanies/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var company = await _context.HostCompanies.FindAsync(id);
            if (company != null)
            {
                _context.HostCompanies.Remove(company);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
