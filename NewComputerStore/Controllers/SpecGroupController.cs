using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    public class SpecGroupController : Controller
    {
        private readonly AppDbContext _context;

        public SpecGroupController(AppDbContext context)
        {
            _context = context;
        }

        // GET: SpecGroup
        public async Task<IActionResult> Index()
        {
            var groups = await _context.SpecGroups
                .Include(g => g.Category)
                .Include(g => g.SpecKeys.OrderBy(k => k.DisplayOrder))
                .OrderBy(g => g.CategoryId).ThenBy(g => g.DisplayOrder)
                .ToListAsync();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(groups);
        }

        // GET: SpecGroup/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View();
        }

        // POST: SpecGroup/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SpecGroup model)
        {
            ModelState.Remove("Category");
            ModelState.Remove("SpecKeys");
            if (ModelState.IsValid)
            {
                // Set DisplayOrder = next in category
                var maxOrder = await _context.SpecGroups
                    .Where(g => g.CategoryId == model.CategoryId)
                    .Select(g => (int?)g.DisplayOrder)
                    .MaxAsync() ?? -1;
                model.DisplayOrder = maxOrder + 1;

                _context.SpecGroups.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Edit), new { id = model.Id });
            }
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(model);
        }

        // GET: SpecGroup/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var group = await _context.SpecGroups
                .Include(g => g.Category)
                .Include(g => g.SpecKeys.OrderBy(k => k.DisplayOrder))
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(group);
        }

        // POST: SpecGroup/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SpecGroup model)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove("Category");
            ModelState.Remove("SpecKeys");
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật nhóm thông số.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            ViewBag.Categories = await _context.Categories.ToListAsync();
            // Reload keys
            model.SpecKeys = await _context.SpecKeys
                .Where(k => k.GroupId == id)
                .OrderBy(k => k.DisplayOrder)
                .ToListAsync();
            return View(model);
        }

        // POST: SpecGroup/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var group = await _context.SpecGroups.FindAsync(id);
            if (group != null)
            {
                _context.SpecGroups.Remove(group);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: SpecGroup/AddKey (AJAX)
        [HttpPost]
        public async Task<IActionResult> AddKey(int groupId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Tên thuộc tính không được để trống.");

            var maxOrder = await _context.SpecKeys
                .Where(k => k.GroupId == groupId)
                .Select(k => (int?)k.DisplayOrder)
                .MaxAsync() ?? -1;

            var key = new SpecKey
            {
                GroupId = groupId,
                Name = name.Trim(),
                DisplayOrder = maxOrder + 1
            };
            _context.SpecKeys.Add(key);
            await _context.SaveChangesAsync();

            return Json(new { id = key.Id, name = key.Name, displayOrder = key.DisplayOrder });
        }

        // POST: SpecGroup/DeleteKey (AJAX)
        [HttpPost]
        public async Task<IActionResult> DeleteKey(int keyId)
        {
            var key = await _context.SpecKeys.FindAsync(keyId);
            if (key != null)
            {
                _context.SpecKeys.Remove(key);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        // POST: SpecGroup/ReorderKeys (AJAX) — accepts ordered list of keyIds
        [HttpPost]
        public async Task<IActionResult> ReorderKeys([FromBody] List<int> orderedIds)
        {
            if (orderedIds == null) return BadRequest();

            var keys = await _context.SpecKeys
                .Where(k => orderedIds.Contains(k.Id))
                .ToListAsync();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                var key = keys.FirstOrDefault(k => k.Id == orderedIds[i]);
                if (key != null) key.DisplayOrder = i;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: SpecGroup/GetSpecsByCategory?categoryId=1 (JSON for AJAX in product forms)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetSpecsByCategory(int categoryId)
        {
            var groups = await _context.SpecGroups
                .Include(g => g.SpecKeys.OrderBy(k => k.DisplayOrder))
                .Where(g => g.CategoryId == categoryId)
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();

            var result = groups.Select(g => new
            {
                groupId = g.Id,
                groupName = g.Name,
                keys = g.SpecKeys.Select(k => new { keyId = k.Id, keyName = k.Name })
            });

            return Json(result);
        }

        // POST: SpecGroup/ClearAllSpecs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAllSpecs()
        {
            // Clear all product specs, spec keys, and spec groups
            var allProductSpecs = await _context.ProductSpecs.ToListAsync();
            _context.ProductSpecs.RemoveRange(allProductSpecs);

            var allKeys = await _context.SpecKeys.ToListAsync();
            _context.SpecKeys.RemoveRange(allKeys);

            var allGroups = await _context.SpecGroups.ToListAsync();
            _context.SpecGroups.RemoveRange(allGroups);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa toàn bộ thông số kỹ thuật. Bạn có thể tự tạo mới ngay bây giờ.";
            return RedirectToAction(nameof(Index));
        }
    }
}
