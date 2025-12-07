using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CAT.AID.Web.Models;

namespace CAT.AID.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // --------------------------------------------------------
        // USERS LIST
        // --------------------------------------------------------
        public IActionResult Users()
        {
            return View(_userManager.Users.ToList());
        }

        // --------------------------------------------------------
        // CREATE USER
        // --------------------------------------------------------
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApplicationUser model, string password, string role)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            model.UserName = model.Email; // keep login consistency

            var result = await _userManager.CreateAsync(model, password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(model);
            }

            await _userManager.AddToRoleAsync(model, role);
            return RedirectToAction(nameof(Users));
        }

        // --------------------------------------------------------
        // EDIT USER
        // --------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.CurrentRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUser model, string role)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.Location = model.Location;

            await _userManager.UpdateAsync(user);

            var existingRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
            await _userManager.AddToRoleAsync(user, role);

            return RedirectToAction(nameof(Users));
        }

        // --------------------------------------------------------
        // RESET PASSWORD
        // --------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            TempData["msg"] = result.Succeeded
                ? "Password updated successfully!"
                : string.Join(", ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Edit), new { id });
        }

        // --------------------------------------------------------
        // DELETE USER
        // --------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Prevent accidental removal of full admin
            if ((await _userManager.GetRolesAsync(user)).Contains("Admin"))
            {
                TempData["msg"] = "Admin user cannot be deleted!";
                return RedirectToAction(nameof(Users));
            }

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(Users));
        }

        // --------------------------------------------------------
        // LOCK & UNLOCK
        // --------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnd = DateTime.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Users));
        }
    }
}
