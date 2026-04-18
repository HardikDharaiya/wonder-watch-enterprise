using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WonderWatch.Application.DTOs;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Enums;

namespace WonderWatch.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/membership")]
    public class MembershipPlanController : Controller
    {
        private readonly IMembershipService _membershipService;

        public MembershipPlanController(IMembershipService membershipService)
        {
            _membershipService = membershipService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var plans = await _membershipService.GetAllPlansAsync();
            return View(plans);
        }

        [HttpGet("create")]
        public IActionResult Create()
        {
            return View(new CreateMembershipPlanDto());
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMembershipPlanDto dto)
        {
            if (ModelState.IsValid)
            {
                await _membershipService.CreatePlanAsync(dto);
                // Notification message could go to TempData here
                TempData["SuccessMessage"] = "Membership Plan created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        [HttpGet("edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var plan = await _membershipService.GetPlanByIdAsync(id);
            if (plan == null) return NotFound();

            var dto = new CreateMembershipPlanDto
            {
                Tier = plan.Tier,
                Name = plan.Name,
                Price = plan.Price,
                BillingCycle = plan.BillingCycle,
                Features = plan.Features,
                IsActive = plan.IsActive
            };

            return View(dto);
        }

        [HttpPost("edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CreateMembershipPlanDto dto)
        {
            if (ModelState.IsValid)
            {
                await _membershipService.UpdatePlanAsync(id, dto);
                TempData["SuccessMessage"] = "Membership Plan updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        [HttpPost("toggle/{id:guid}")]
        public async Task<IActionResult> ToggleActive(Guid id)
        {
            await _membershipService.TogglePlanActiveAsync(id);
            TempData["SuccessMessage"] = "Plan status toggled successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _membershipService.DeletePlanAsync(id);
            TempData["SuccessMessage"] = "Membership Plan deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
