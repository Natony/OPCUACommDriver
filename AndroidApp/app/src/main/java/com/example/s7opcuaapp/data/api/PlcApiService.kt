package com.example.s7opcuaapp.data.api

import retrofit2.Response
import retrofit2.http.*

/**
 * Retrofit interface for PLC API endpoints
 */
interface PlcApiService {

    // ============== PLC Endpoints ==============

    /**
     * Get all PLCs
     */
    @GET("api/plcs")
    suspend fun getAllPlcs(): Response<ApiResponse<List<PlcDto>>>

    /**
     * Get PLC by ID
     */
    @GET("api/plcs/{plcId}")
    suspend fun getPlcById(@Path("plcId") plcId: String): Response<ApiResponse<PlcDto>>

    /**
     * Get connection status of all PLCs
     */
    @GET("api/plcs/status")
    suspend fun getPlcStatus(): Response<ApiResponse<List<ConnectionStatusDto>>>

    /**
     * Connect to a PLC
     */
    @POST("api/plcs/{plcId}/connect")
    suspend fun connectPlc(@Path("plcId") plcId: String): Response<ApiResponse<Boolean>>

    /**
     * Disconnect from a PLC
     */
    @POST("api/plcs/{plcId}/disconnect")
    suspend fun disconnectPlc(@Path("plcId") plcId: String): Response<ApiResponse<Boolean>>

    /**
     * Connect to all PLCs
     */
    @POST("api/plcs/connect-all")
    suspend fun connectAllPlcs(): Response<ApiResponse<Int>>

    /**
     * Disconnect from all PLCs
     */
    @POST("api/plcs/disconnect-all")
    suspend fun disconnectAllPlcs(): Response<ApiResponse<Boolean>>

    // ============== Tag Endpoints ==============

    /**
     * Get all tags from all PLCs
     */
    @GET("api/tags")
    suspend fun getAllTags(): Response<ApiResponse<List<TagDto>>>

    /**
     * Get tags for a specific PLC
     */
    @GET("api/tags/plc/{plcId}")
    suspend fun getTagsByPlc(@Path("plcId") plcId: String): Response<ApiResponse<List<TagDto>>>

    /**
     * Get subscribed tags (from connected PLCs only)
     */
    @GET("api/tags/subscribed")
    suspend fun getSubscribedTags(): Response<ApiResponse<List<TagDto>>>

    /**
     * Read a tag value
     */
    @GET("api/tags/{plcId}/{nodeId}/read")
    suspend fun readTag(
        @Path("plcId") plcId: String,
        @Path("nodeId", encoded = true) nodeId: String
    ): Response<ApiResponse<TagDto>>

    /**
     * Write a tag value
     */
    @POST("api/tags/{plcId}/{nodeId}/write")
    suspend fun writeTag(
        @Path("plcId") plcId: String,
        @Path("nodeId", encoded = true) nodeId: String,
        @Body request: WriteTagRequest
    ): Response<ApiResponse<Boolean>>
}
