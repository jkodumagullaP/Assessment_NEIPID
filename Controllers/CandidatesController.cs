using CAT.AID.Models;
using CAT.AID.Models.DTO;
using CAT.AID.Web.Data;
using CAT.AID.Web.Models;
using CAT.AID.Web.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ComparisonReportVM = CAT.AID.Web.Models.DTO.ComparisonReportVM;

namespace CAT.AID.Web.Controllers
{
    [Authorize(Roles = "LeadAssessor")]
    public class CandidatesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public CandidatesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        // ---------------- INDEX ----------------
        public async Task<IActionResult> Index()
        {
            var candidates = await _db.Candidates
                .Where(x => !x.IsArchived)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(candidates);
        }

        // ---------------- CREATE (GET) ----------------
        public IActionResult Create()
        {
            return View(new Candidate
            {
                DOB = DateTime.Today
            });
        }

        // ---------------- CREATE (POST) ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Candidate m)
        {
            if (!ModelState.IsValid)
            {
                TempData["msg"] = "⚠ Please complete all required fields.";
                return View(m);
            }

            bool exists = await _db.Candidates
                .AnyAsync(x => x.FullName == m.FullName && x.DOB == m.DOB);

            if (exists)
            {
                TempData["msg"] = "⚠ Candidate already exists with same name & date of birth.";
                return View(m);
            }

            try
            {
                _db.Candidates.Add(m);
                await _db.SaveChangesAsync();
                TempData["msg"] = "✔ Candidate saved successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["msg"] = "❌ Error saving candidate.";
                return View(m);
            }
        }

        // ---------------- EDIT (GET) ----------------
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.Candidates.FindAsync(id);
            return m == null ? NotFound() : View(m);
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Candidate m)
        {
            if (!ModelState.IsValid)
                return View(m);

            try
            {
                _db.Update(m);
                await _db.SaveChangesAsync();
                TempData["msg"] = "✔ Candidate updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["msg"] = "❌ Failed to update candidate.";
                return View(m);
            }
        }

        // ---------------- ARCHIVE ----------------
        public async Task<IActionResult> Archive(int id)
        {
            var m = await _db.Candidates.FindAsync(id);
            if (m == null) return NotFound();

            m.IsArchived = true;
            await _db.SaveChangesAsync();
            TempData["msg"] = "📌 Candidate archived.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- ASSIGN ASSESSOR (GET) ----------------
        public async Task<IActionResult> Assign(int id)
        {
            var candidate = await _db.Candidates.FindAsync(id);
            if (candidate == null) return NotFound();

            var assessors = await _userManager.GetUsersInRoleAsync("Assessor");

            // Filter by location — if none match, show all
            var filtered = assessors
                .Where(x => x.Location == candidate.CommunicationAddress)
                .ToList();

            if (!filtered.Any())
                filtered = assessors.ToList();   // fallback to all assessors

            ViewBag.Assessors = filtered;
            ViewBag.Candidate = candidate;

            return View();
        }

        // ---------------- ASSIGN ASSESSOR (POST) ----------------
        [HttpPost]
        public async Task<IActionResult> Assign(int id, string assessorId)
        {
            var candidate = await _db.Candidates.FindAsync(id);
            if (candidate == null)
                return NotFound();

            // assessorId already comes as string (GUID)
            if (string.IsNullOrWhiteSpace(assessorId))
                return BadRequest("Invalid assessor id");

            // Logged-in user ID is also a string
            var leadIdStr = _userManager.GetUserId(User);
            if (leadIdStr == null)
                return Unauthorized();

            var assessment = new Assessment
            {
                CandidateId = id,
                AssessorId = assessorId,        // string GUID
                LeadAssessorId = leadIdStr,     // string GUID
                Status = AssessmentStatus.Assigned,
                CreatedAt = DateTime.UtcNow
            };

            _db.Assessments.Add(assessment);
            await _db.SaveChangesAsync();

            TempData["msg"] = "✔ Assessor assigned successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- COMPARISON ----------------
        [HttpGet]
        public async Task<IActionResult> Compare(int candidateId, List<int> ids)
        {
            if (candidateId == 0 || ids == null || ids.Count < 2)
            {
                TempData["msg"] = "⚠ Select at least two assessments to compare.";
                return RedirectToAction("MyTasks", "Assessments");
            }

            var candidate = await _db.Candidates.FindAsync(candidateId);
            if (candidate == null)
            {
                TempData["msg"] = "⚠ Candidate not found.";
                return RedirectToAction("MyTasks", "Assessments");
            }

            var assessments = await _db.Assessments
                .Where(a => ids.Contains(a.Id) &&
                            a.CandidateId == candidateId &&
                            (a.Status == AssessmentStatus.Approved ||
                             a.Status == AssessmentStatus.Submitted))
                .OrderBy(a => a.SubmittedAt)
                .ToListAsync();

            if (assessments.Count < 2)
            {
                TempData["msg"] = "⚠ No comparable assessments found.";
                return RedirectToAction("MyTasks", "Assessments");
            }

            var vm = new ComparisonReportVM
            {
                CandidateId = candidateId,
                CandidateName = candidate.FullName,
                AssessmentIds = ids,
                Assessments = assessments
            };

            return View("CompareReport", vm);
        }
    }
}
