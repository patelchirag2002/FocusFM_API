﻿using System.Data;
using System.IdentityModel.Tokens.Jwt;
using ClosedXML.Excel;
using FocusFM.Common.CommonMethod;
using FocusFM.Common.Enum;
using FocusFM.Common.Helpers;
using FocusFM.Model.CommonPagination;
using FocusFM.Model.Providers;
using FocusFM.Model.Site;
using FocusFM.Model.Site.Floor;
using FocusFM.Model.Site.Meter;
using FocusFM.Model.MeterOccupier;
using FocusFM.Service.JWTAuthentication;
using FocusFM.Service.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusFMAPI.Controllers
{
    [Route("api/site")]
    [ApiController]
    [Authorize]
    public class SiteController : ControllerBase
    {
        #region Fields
        private readonly ISiteService _SiteService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IJWTAuthenticationService _jwtAuthenticationService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _hostingEnvironment;

        #endregion

        #region Constructor
        public SiteController
        (
            ISiteService SiteService,
            IHttpContextAccessor httpContextAccessor,
            IJWTAuthenticationService jwtAuthenticationService,
            IConfiguration config,
            IWebHostEnvironment hostingEnvironment
        )
        {
            _SiteService = SiteService;
            _httpContextAccessor = httpContextAccessor;
            _jwtAuthenticationService = jwtAuthenticationService;
            _config = config;
            _hostingEnvironment = hostingEnvironment;
        }
        #endregion

        [HttpPost("save")]
        public async Task<BaseApiResponse> SaveSite([FromForm] SiteRequestModel model)
        {
            IFormFile file = model.File;
            ApiResponse<SiteResponseModel> response = new ApiResponse<SiteResponseModel>();
            if (model.SiteId == null)
            {
                if (file == null || file.Length == 0)
                {
                    response.Data = null;
                    response.Success = false;
                    response.Message = ErrorMessages.NoFile;
                    return response;
                }
            }
            long UserId = 0;
            string fileName = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Extract user ID from the token claims
                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            if (file != null)
            {
                string[] arrExtension = _config["FileConfiguration:AllowedProfileFileFormat"].Split(",");
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!arrExtension.Contains(extension))
                {
                    response.Success = false;
                    response.Message = ErrorMessages.InvalidImageFile;
                    return response;
                }

                long allowedSize = long.Parse(_config["FileConfiguration:AllowedProfileFileSize"]);

                if (file.Length > allowedSize)
                {
                    response.Success = false;
                    response.Message = ErrorMessages.FileSizeExceeds;
                    return response;
                }

                fileName = await CommonMethods.UploadDocument(file, Path.Combine(_hostingEnvironment.WebRootPath,_config["FileConfiguration:SiteImageFilePath"]));
            }
            var result = await _SiteService.SaveSite(model, UserId, fileName);
            var issend = false;
            if (result == Status.Success)
            {
                if (model.SiteId > 0)
                {
                    response.Message = ErrorMessages.UpdateSiteSuccess;
                    response.Success = true;
                }
                else
                {
                    response.Message = ErrorMessages.SaveSiteSuccess;
                    response.Success = true;
                }
            }
            else if (result == Status.AlredyExists)
            {
                response.Message = ErrorMessages.SiteExists;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpPost("list")]
        public async Task<ApiResponse<SiteResponseModel>> GetSiteList(CommonPaginationModel model)
        {
            ApiResponse<SiteResponseModel> response = new ApiResponse<SiteResponseModel>() { Data = new List<SiteResponseModel>() };
            var result = await _SiteService.GetSiteList(model);
            foreach (var record in result)
            {
                // Example: Update file path if it exists
                if (record.SiteImage != null)
                {
                    string originalPath = record.SiteImage.ToString();

                    // Example: Replace part of the path or add prefix
                    //record.SiteImage = originalPath.Replace(Directory.GetCurrentDirectory(), _config["AppSettings:APIURL"]);
                    record.SiteImage = Path.Combine(_config["AppSettings:APIURL"], _config["FileConfiguration:SiteImageFilePath"], record.SiteImage);
                    record.SiteImage = record.SiteImage.Replace("\\", "/");
                    //record.SiteImage = record.SiteImage.Replace("/wwwroot", "");
                }
            }
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpDelete("delete/{id}")]
        public async Task<BaseApiResponse> DeleteSite(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.DeleteSite(id,UserId);
            if (result == Status.Success)
            {
                response.Message = ErrorMessages.DeleteSiteSuccess;
                response.Success = true;
            }
            else if (result == Status.DataInUse)
            {
                response.Message = ErrorMessages.LinkedData;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpGet("active-inactive/{id}")]
        public async Task<BaseApiResponse> ActiveInActiveSite(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }

            var result = await _SiteService.ActiveInActiveSite(id,UserId);
            if (result == Status.Success)
            {
                if (result == ActiveStatus.Inactive)
                {
                    response.Message = ErrorMessages.SiteInactive;
                    response.Success = true;
                }
                else if (result == ActiveStatus.Active)
                {
                    response.Message = ErrorMessages.SiteActive;
                    response.Success = true;
                }
            }
            else if (result == Status.DataInUse)
            {
                response.Message = ErrorMessages.LinkedData;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        #region Site Floor Methods

        [HttpPost("floor/save")]
        public async Task<BaseApiResponse> SaveFloor(SiteFloorRequestModel model)
        {
            ApiResponse<SiteFloorResponseModel> response = new ApiResponse<SiteFloorResponseModel>();
            long UserId = 0;
            string fileName = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Extract user ID from the token claims
                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.SaveFloor(model, UserId);
            var issend = false;
            if (result == Status.Success)
            {
                if (model.FloorId > 0)
                {
                    response.Message = ErrorMessages.UpdateFloorSuccess;
                    response.Success = true;
                }
                else
                {
                    response.Message = ErrorMessages.SaveFloorSuccess;
                    response.Success = true;
                }
            }

            else if (result == Status.AlredyExists)
            {
                response.Message = ErrorMessages.FloorExists;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpPost("floor/list")]
        public async Task<ApiResponse<SiteFloorResponseModel>> GetFloorList(GetSiteFloorModel model)
        {
            ApiResponse<SiteFloorResponseModel> response = new ApiResponse<SiteFloorResponseModel>() { Data = new List<SiteFloorResponseModel>() };
            var result = await _SiteService.GetFloorList(model);
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpDelete("floor/delete/{id}")]
        public async Task<BaseApiResponse> DeleteFloor(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.DeleteFloor(id, UserId);
            if (result == 1)
            {
                response.Message = ErrorMessages.DeleteFloorSuccess;
                response.Success = true;
            } 
            else if (result == 0)
            {
                response.Message = ErrorMessages.YouHaveNotDeleteFloor;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpGet("floor/active-inactive/{id}")]
        public async Task<BaseApiResponse> ActiveInActiveFloor(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }

            var result = await _SiteService.ActiveInActiveFloor(id, UserId);
            if (result == ActiveStatus.Inactive)
            {
                response.Message = ErrorMessages.FloorInactive;
                response.Success = true;
            }
            else if (result == ActiveStatus.Active)
            {
                response.Message = ErrorMessages.FloorActive;
                response.Success = true;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpPost("floor/dropdown")]
        public async Task<ApiResponse<FloorDropdownResponseModel>> GetFloorDropdown(long SiteId)
        {
            ApiResponse<FloorDropdownResponseModel> response = new ApiResponse<FloorDropdownResponseModel>() { Data = new List<FloorDropdownResponseModel>() };
            var result = await _SiteService.GetFloorDropdown(SiteId);
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        #endregion

        #region Meter Reading Type
        [HttpPost("meterreadingtype/dropdown")]
        public async Task<ApiResponse<MeterReadingTypeResponseModel>> GetMeterReadingTypeDropdown()
        {
            ApiResponse<MeterReadingTypeResponseModel> response = new ApiResponse<MeterReadingTypeResponseModel>() { Data = new List<MeterReadingTypeResponseModel>() };
            var result = await _SiteService.GetMeterReadingTypeDropdown();
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpPost("metertype/dropdown")]
        public async Task<ApiResponse<MeterTypeResponseModel>> GetMeterTypeDropdown()
        {
            ApiResponse<MeterTypeResponseModel> response = new ApiResponse<MeterTypeResponseModel>() { Data = new List<MeterTypeResponseModel>() };
            var result = await _SiteService.GetMeterTypeDropdown();
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpPost("meter/save")]
        public async Task<BaseApiResponse> SaveMeter(MeterRequestModel model)
        {
            ApiResponse<MeterResponseModel> response = new ApiResponse<MeterResponseModel>();
            long UserId = 0;
            string fileName = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Extract user ID from the token claims
                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.SaveMeter(model, UserId);
            var issend = false;
            if (result == Status.Success)
            {
                if (model.MeterId > 0)
                {
                    response.Message = ErrorMessages.UpdateMeterSuccess;
                    response.Success = true;
                }
                else
                {
                    response.Message = ErrorMessages.SaveMeterSuccess;
                    response.Success = true;
                }
            }

            else if (result == Status.AlredyExists)
            {
                response.Message = ErrorMessages.MeterExists;
                response.Success = false;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpPost("meter/list")]
        public async Task<ApiResponse<MeterResponseModel>> GetMeterList(GetMeterListRequestModel model)
        {
            ApiResponse<MeterResponseModel> response = new ApiResponse<MeterResponseModel>() { Data = new List<MeterResponseModel>() };
            var result = await _SiteService.GetMeterList(model);
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpDelete("meter/delete/{id}")]
        public async Task<BaseApiResponse> DeleteMeter(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.DeleteMeter(id, UserId);
            if (result == 0)
            {
                response.Message = ErrorMessages.DeleteMeterSuccess;
                response.Success = true;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpGet("meter/active-inactive/{id}")]
        public async Task<BaseApiResponse> ActiveInActiveMeter(long id)
        {
            BaseApiResponse response = new BaseApiResponse();
            long UserId = 0;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }

            var result = await _SiteService.ActiveInActiveMeter(id, UserId);
            if (result == ActiveStatus.Inactive)
            {
                response.Message = ErrorMessages.MeterInactive;
                response.Success = true;
            }
            else if (result == ActiveStatus.Active)
            {
                response.Message = ErrorMessages.MeterActive;
                response.Success = true;
            }
            else
            {
                response.Message = ErrorMessages.SomethingWentWrong;
                response.Success = false;
            }
            return response;
        }

        [HttpPost("meter/export")]
        public async Task<IActionResult> ExportMeterData(MeterExportRequestModel model)
        {
            var result = await _SiteService.GetMeterExportData(model);
            if (result != null)
            {
                DataTable dt = Utility.ToDataTable(result);
                dt.Columns["SiteName"].ColumnName = "Site Name";
                dt.Columns["MeterOccupierType"].ColumnName = "Paid By";
                dt.Columns["MeterReadingType"].ColumnName = "Unit";
                dt.Columns["MeterType"].ColumnName = "Meter Type";
                dt.Columns["ProviderName"].ColumnName = "Provider Name";
                dt.Columns["MeterName"].ColumnName = "Meter Name";
                dt.Columns["MeterNumber"].ColumnName = "Meter Id";
                dt.Columns["FloorName"].ColumnName = "Floor Name";
                dt.Columns["LandlordName"].ColumnName = "Landlord Name";
                dt.Columns["TenantName"].ColumnName = "Tenant Name";
                // Get Excel as byte array
                var fileBytes = ExportHelper.ExportToExcel(dt, "Meter List", "Meters");

                // Return the file directly without stream
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Meters.xlsx");
            }
            return BadRequest();
        }


        [HttpPost("meter/dropdown")]
        public async Task<ApiResponse<MeterDropdownResponseModel>> GetMeterDropdown(MeterDropdownRequestModel model)
        {
            ApiResponse<MeterDropdownResponseModel> response = new ApiResponse<MeterDropdownResponseModel>() { Data = new List<MeterDropdownResponseModel>() };
            var result = await _SiteService.GetMeterDropdown(model);
            if (result != null)
            {
                response.Data = result;
            }
            response.Success = true;
            return response;
        }

        [HttpPost("meter/import")]
        public async Task<ApiResponse<MeterBulkImportResponseModel>> ValidateInsertMeterBulkImport(IFormFile file)
        {
            ApiResponse<MeterBulkImportResponseModel> response = new ApiResponse<MeterBulkImportResponseModel>() { Data = new List<MeterBulkImportResponseModel>() };
            long UserId = 0;
            if (file == null || file.Length == 0)
            {
                response.Data = null;
                response.Success = false;
                response.Message = ErrorMessages.NoFile;
                return response;
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Read headers and map to model properties
            var headerMap = worksheet.Row(1)
                                     .CellsUsed()
                                     .ToDictionary(cell => cell.GetValue<string>().Trim(), cell => cell.Address.ColumnNumber);

            // Prepare a list of MeterDataRow models
            var model = new List<MeterBulkImportRequestModel>();

            // Loop through rows (skip header)
            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var meterData = new MeterBulkImportRequestModel
                {
                    // Map Excel column names to model properties
                    MeterName = row.Cell(headerMap["Meter Name"]).GetValue<string>().Trim(),
                    MeterNumber = row.Cell(headerMap["Meter Id"]).GetValue<string>().Trim(),
                    SiteName = row.Cell(headerMap["Site Name"]).GetValue<string>().Trim(),
                    FloorName = row.Cell(headerMap["Floor Name"]).GetValue<string>().Trim(),
                    ProviderName = row.Cell(headerMap["Provider Name"]).GetValue<string>().Trim(),
                    MeterReadingType = row.Cell(headerMap["Unit"]).GetValue<string>().Trim(),
                    MeterType = row.Cell(headerMap["Meter Type"]).GetValue<string>().Trim(),
                    MeterOccupierType = row.Cell(headerMap["Paid By"]).GetValue<string>().Trim(),
                    LandlordName = row.Cell(headerMap["Landlord Name"]).GetValue<string>().Trim(),
                    TenantName = row.Cell(headerMap["Tenant Name"]).GetValue<string>().Trim(),
                };

                model.Add(meterData);
            }

            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                // Parse the JWT token
                var token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var j = JsonConvert.DeserializeObject<Dictionary<string, string>>(jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name").Value);

                UserId = long.TryParse(j["UserId"], out var val) ? val : 0;
            }
            var result = await _SiteService.ValidateInsertMeterBulkImport(model,UserId);
            if (result != null)
            {
                response.Data = result;
                if (response.Data.Count <= 0)
                {
                    response.Success = true;
                    response.Message = ErrorMessages.MeterBulkImportSuccess;

                }
                else
                {
                    response.Success = false;
                    response.Message = ErrorMessages.MeterBulkImportFail;
                }
            }
            return response;
        }

        [HttpGet("meter/download-sample")]
        public IActionResult GetSampleImportFile()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "SampleFiles", "MeterBuIkImportSampleFile.xlsx");

            if (!System.IO.File.Exists(filePath))
                return NotFound("Sample file not found.");

            var mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; // or application/vnd.openxmlformats-officedocument.spreadsheetml.sheet for Excel
            var fileName = "MeterBuIkImportSampleFile.xlsx";

            return PhysicalFile(filePath, mimeType, fileName);
        }

        #endregion
    }
}
