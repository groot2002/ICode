﻿using API.Extension;
using API.Filter;
using API.Helper;
using API.Models.DTO;
using API.Services;
using AutoMapper;
using CodeStudy.Models;
using Data.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("submissions")]
    [ApiController]
    public class SubmissionController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;
        public SubmissionController(ISubmissionService submissionService, IMapper mapper)
        {
            _submissionService = submissionService;
        }

        [Authorize(Roles = "Admin")]
        [QueryConstraint(Key = "sort", Value = "user, problem, language, status, date", Retrict = false)]
        [QueryConstraint(Key = "orderBy", Value = "asc, desc", Depend = "sort")]
        public async Task<IActionResult> Find(int? page = null, int pageSize = 5, string user = "", string problem = "", string language = "", bool? status = null, DateTime? date = null, string sort = "", string orderBy = "")
        {
            if (page == null)
            {
                return Ok(_submissionService.GetSubmissionByFilter(user, problem, language, status, date, sort, orderBy));
            }
            else
            {
                return Ok(await _submissionService.GetPageByFilter((int)page, pageSize, user, problem, language, status, date, sort, orderBy));
            }
        }

        [HttpGet("{ID}")]
        [Authorize]
        public IActionResult GetByID(string ID)
        {
            SubmissionDTO submission = _submissionService.GetDetail(ID);
            if (submission == null)
            {
                return NotFound(new ErrorResponse
                {
                    error = "Resource not found.",
                    detail = "Submission does not exist."
                });
            }
            else
            {
                if (User.FindFirst(Constant.ROLE).Value == Constant.ADMIN || submission.User.ID == User.FindFirst(Constant.ID).Value)
                {
                    return Ok(new
                    {
                        submission = submission,
                        detail = _submissionService.GetSubmitDetail(ID)
                    });
                }
                else
                {
                    return Forbid();
                }
            }
        }

        [HttpDelete("{ID}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string ID)
        {
            if (!await _submissionService.Remove(ID))
            {
                return NotFound(new ErrorResponse
                {
                    error = "Resource not found.",
                    detail = "Submission does not exist."
                });
            }
            else
            {
                return Ok();
            }
        }
    }
}
