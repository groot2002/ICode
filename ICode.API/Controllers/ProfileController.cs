﻿using API.Filter;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CodeStudy.Models;
using Data.Entity;
using ICode.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.DTO;
using Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("me")]
    [Authorize]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ISubmissionService _submissionService;
        private readonly IMapper _mapper;
        public ProfileController(IUserService userService, ISubmissionService submissionService,IMapper mapper)
        {
            _userService = userService;
            _submissionService = submissionService;
            _mapper = mapper;
        }

        [HttpGet]
        public IActionResult GetProfile()
        {
            User user = _userService.FindByID(User.FindFirst(Constant.ID).Value);
            if (user == null)
            {
                return NotFound(new ErrorResponse
                {
                    error = "Resource not found.",
                    detail = "User does not exist."
                });
            }
            return Ok(_mapper.Map<User, UserDTO>(user));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromForm] UserUpdate input, [FromForm] IFormFile avatar, [FromServices] IUploadService uploadService)
        {
            User user = _userService.FindByID(User.FindFirst(Constant.ID).Value);
            if (user == null)
            {
                return NotFound(new ErrorResponse
                {
                    error = "Resource not found.",
                    detail = "User does not exist."
                });
            }
            if (_userService.Exist(input.Username, input.Username))
            {
                return Conflict(new ErrorResponse
                {
                    error = "Update failed.",
                    detail = "Username or email already exist."
                }); ;
            }
            if (avatar != null && avatar.Length > 0)
            {
                using (var stream = avatar.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(avatar.Name, stream),
                        Folder = "ICode"
                    };
                    string avtUrl = await uploadService.UploadAsync(uploadParams);
                    if (avtUrl == null)
                    {
                        throw new Exception("Upload file faild");
                    }
                    else
                    {
                        input.UploadImage = avtUrl;
                    }
                }
            }
            await _userService.Update(User.FindFirst(Constant.ID).Value, input);
            return NoContent();
        }

        [HttpGet("problems")]
        [QueryConstraint(Key = "sort", Value = "name, tag, date", Retrict = false)]
        [QueryConstraint(Key = "orderBy", Value = "asc, desc", Depend = "sort")]
        public async Task<IActionResult> GetProblemOfUser(string status = "author", string name = "", string tag = "", DateTime? date = null, string sort = "", string orderBy = "")
        {
            User user = _userService.FindByID(User.FindFirst(Constant.ID).Value);
            if (user == null)
            {
                return NotFound(new ErrorResponse
                {
                    error = "Resource not found.",
                    detail = "User does not exist."
                });
            }
            else
            {
                switch (status)
                {
                    case "author":
                        return Ok(_userService.GetProblemCreatedByUser(User.FindFirst(Constant.ID).Value, name, tag, date, sort, orderBy));
                    case "solved":
                        return Ok(await _userService.GetProblemSolvedByUser(User.FindFirst(Constant.ID).Value, name, tag));
                    default:
                        return BadRequest(new ErrorResponse
                        {
                            error = "Invalid action.",
                            detail = $"Status '{status}' is not a valid action."
                        });
                }
            }
        }

        [HttpGet("submissions")]
        [QueryConstraint(Key = "sort", Value = "problem, language, status, date", Retrict = false)]
        [QueryConstraint(Key = "orderBy", Value = "asc, desc", Depend = "sort")]
        public async Task<IActionResult> GetSubmissionsOfUser(int? page = null, int pageSize = 5, string problem = "", string language = "", bool? status = null, DateTime? date = null, string sort = "", string orderBy = "")
        {
            if (page == null)
            {
                return Ok(_userService.GetSubmitOfUser(User.FindFirst(Constant.ID).Value, problem, language, status, date, sort, orderBy));
            }
            else
            {
                return Ok(await _userService.GetPageSubmitOfUser((int)page, pageSize, User.FindFirst(Constant.ID).Value, problem, language, status, date, sort, orderBy));
            }
        }
    }
}
